# Companies House API — Data Reference

*Reference summary for the credit risk extension's UK counterparty enrichment*

## Overview

- **Base URL:** `https://api.company-information.service.gov.uk`
- **Auth:** HTTP Basic, API key as username, no password required
- **Format:** JSON, read-only (no filing/updating via API)
- **Rate limit:** 600 requests per 5 minutes per key (429 + `Retry-After` header if exceeded)
- **Cost:** Free — no per-call charges
- **Coverage:** England & Wales, Scotland, Northern Ireland only. No overseas entities.

## Endpoints and data returned

### Search
| Endpoint | Purpose |
|---|---|
| `/search/companies` | Company name search, up to 50 hits/call, paginated |
| `/search/officers` | Search by director/officer name |
| `/search/disqualified-officers` | Search disqualified directors |

### Company profile — `/company/{number}`
- Status, type, incorporation date, registered office address
- SIC codes (industry classification)
- Accounts filing schedule (incl. `next_due`)
- Confirmation-statement schedule
- Previous company names
- Jurisdiction (England & Wales / Scotland / Northern Ireland)
- Insolvency-history flag
- **Detailed status field** — eight possible values: `active`, `dissolved`, `liquidation`, `receivership`, `administration`, `voluntary-arrangement`, `converted-closed`, `removed`, plus a free-text `company_status_detail` (e.g. "active - proposal to strike off") for in-flight cases

### Officers — `/company/{number}/officers`
- Directors, secretaries, and auditors — **current and resigned, by default, in the same response**
- Fields: `name`, `officer_role`, `appointed_on`, `resigned_on` (where applicable), `nationality`, `country_of_residence`, `correspondence_address`
- Date of birth returned as **month + year only** — day is legally suppressed under residential-address protection rules (Companies Act 2006 s.243)
- New `identity_verification_status` field (rolling out from late 2025 under ECCTA) showing whether a director/PSC has completed identity verification

**Filtering officers:**
- No parameters → returns everything, active and resigned, all roles
- `?filter=active` → excludes resigned officers (still returns all roles; filter `officer_role=director` client-side if needed)
- Avoid `register_view`/`register_type` for this purpose — both are required together, only work if the specific register happens to be held at Companies House, and return 404 for many companies otherwise. This is a legacy statutory-register feature, not a general active/resigned filter.

### Persons with Significant Control (PSC) — `/company/{number}/persons-with-significant-control`
- Structured beneficial-ownership register: name, kind, nature of control, notification dates
- Fully public in the UK (unlike the EU post-CJEU C-37/20, which restricted PSC-equivalent data to AML-obliged entities)
- **Important:** PSC is a controlling-influence register, not an equity register — it can disagree with actual shareholding (e.g. a 10% direct shareholder may not appear in PSC; a corporate trustee with control rights may appear in PSC without being a shareholder)

### Filing history — `/company/{number}/filing-history`
- Every filing: category, type code, date, document ID
- Actual documents fetchable separately as PDF or XHTML/iXBRL bytes

### Charges — `/company/{number}/charges`
- Registered fixed/floating charges (mortgages, debentures)
- Persons entitled, status (outstanding / satisfied / part-satisfied), creation date

## What's notably absent

- **No structured shareholders/equity endpoint** — the statutory members register only exists inside the Confirmation Statement (CS01) filing document, which must be fetched and parsed manually
- No credit scores, revenue estimates, or financial ratios
- No contact details (phone/email)
- No data on non-UK entities

## Access exceptions

Only two things are gated:
1. Bulk annual data dump downloads carry a fee (the same data is available free via the per-request API)
2. Two internal-only feeds: the PSC nominee register and the names-by-residential-address feed

## Data quirks relevant to enrichment logic

**Company number formats are not all 8-digit numerics:**
| Prefix | Entity type |
|---|---|
| 8 digits | Standard England & Wales / Wales-only company |
| `SC` + 6 digits | Scottish company |
| `NI` + 6 digits | Northern Ireland |
| `OC` + 6 digits | LLP |
| `LP` + 6 digits | Limited Partnership |
| `RC` + 6 digits | Registered Society |
| `FC` + 6 digits | Foreign company branch (no UK accounts filed — statutory dossier is at the home jurisdiction) |
| `NF` + 6 digits | Northern Ireland LLP |

Code that assumes all-numeric company numbers will silently exclude Scottish companies and every LLP in the country.

## Relevance to credit risk tiering

- **Director resignation patterns** are visible out of the box via `appointed_on`/`resigned_on` on the officers endpoint — useful signal for detecting management turnover ahead of other red flags (charges, status changes)
- **Detailed status field** (`company_status_detail`) surfaces early-warning states like "proposal to strike off" that a simple active/dissolved binary would miss
- **Charges data** gives visibility into existing debt obligations (mortgages/debentures) against a counterparty
- **PSC vs shareholding mismatch** should be treated as a known limitation, not a bug, when building ownership-based risk logic
