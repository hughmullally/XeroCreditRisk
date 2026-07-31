# Creditsafe API — Research Notes

## What Creditsafe Is

A business credit reference agency (UK/international) — similar in purpose to
Companies House's data, but instead of just filing/insolvency status, they
provide an actual computed **credit score and recommended credit limit** for
companies, based on their own risk models, plus directors' info, balance
sheet data, and negative information (CCJs, etc.).

## The Connect API

### Base URLs

| Environment | URL |
|---|---|
| Production | `https://connect.creditsafe.com/v1` |
| Sandbox | `https://connect.sandbox.creditsafe.com/v1` |

### Authentication

- `POST /authenticate` with `username` + `password` in the body
- Returns a Bearer JWT, valid for 1 hour (re-authenticate to get a fresh one)
- Used as `Authorization: Bearer <token>` on all subsequent calls
- Rate limits: max 5 identical invalid requests per 2 minutes (429), and a
  lockout after 10,000 requests in 5 minutes

### Key Endpoints

| Endpoint | Purpose |
|---|---|
| `GET /companies?countries=GB&regNo=...` | Search/look up a company (by UK company number, name, etc.) — returns a `connectId` |
| `GET /companies/{connectId}` | Full credit report: credit score, credit limit, directors, balance sheet, negative info. Supports `Accept: application/json+pdf` for a Base64 PDF report too |

## International Coverage

Not UK-only — the `countries` parameter on `GET /companies` (ISO-2 codes)
makes multi-country lookup a first-class part of the API. Reported coverage
(figures vary by source, all Creditsafe's own):

| Coverage type | Extent |
|---|---|
| Owned/direct database | 100+ countries, 365M+ business reports |
| Instant international reports | 200+ countries (via ~9,000 data sources: registries, courts, local partners) |
| Live real-time data feeds | 70+ countries |
| Portfolio monitoring | 49 countries (North America, Europe, Asia-Pacific) |

Relevant if this app ever needs to check an overseas parent company or
international counterparty, not just UK contacts. Worth confirming "instant"
vs. "on-request" availability for any specific country that matters before
relying on it.

## Fit With This App

Maps cleanly onto the existing `ICompaniesHouseService`/`CompaniesHouseService`
pattern — a `CreditsafeService` alongside it, same shape (look up by company
number → get a profile). It would give a genuine third-party credit score,
versus:

- The current `CreditLimitRecommendation` — self-calculated purely from this
  org's own Xero invoice history
- Companies House data — just filing/insolvency status, not a score

## Key Considerations

- **It's a paid commercial product**, not free/public like Companies House —
  requires an actual account/contract with Creditsafe. Signup looks
  sales/demo-led rather than instant self-serve:
  [creditsafe.com/us/en/enterprise/integrations/company-data-api.html](https://www.creditsafe.com/us/en/enterprise/integrations/company-data-api.html)
- Credentials would need Key Vault storage, same as the Xero secrets
- Worth deciding upfront: does this **replace** the current recommended-limit
  logic, or sit **alongside** it as an extra column/data point?

## Sources

- [doc.creditsafe.com](https://doc.creditsafe.com/)
- [Company Credit Report docs](https://doc.creditsafe.com/connect-apis-catalog/product-catalog/creditrisk/creditandrisk/companies/companycreditreport)
- [connect-docs OpenAPI spec (archived)](https://github.com/creditsafe/connect-docs)
- [Creditsafe Data](https://www.creditsafe.com/us/en/more/about/our-data.html)
- [International Company Credit Reports & Credit Scores](https://www.creditsafe.com/us/en/credit-risk/credit-reports/international-credit-reports.html)
- [Business Credit Monitoring & International Company Monitoring](https://www.creditsafe.com/us/en/credit-risk/credit-reports/company-monitoring.html)
