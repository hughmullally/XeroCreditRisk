# Xero Credit Risk

A credit-risk monitoring dashboard built on top of Xero. It connects to a Xero
organisation via OAuth 2.0, reads sales invoice and contact data, and turns it
into a live-updating risk dashboard — enriched with UK company data from
Companies House and pushed in real time via Xero webhooks.

## Connecting to Xero

1. `GET /auth/xero/connect` — redirects to Xero's login/consent screen.
2. Xero redirects back to `GET /auth/xero/callback?code=...`, which exchanges
   the code for tokens and stores them (in-memory; resets on app restart).
3. `DELETE /auth/xero/disconnect` — clears the stored tokens.

Tokens are held against a single hardcoded `default-user` — see
[Key Extension Points](CLAUDE.md#key-extension-points) in `CLAUDE.md` for what
a multi-user deployment would need to change.

## Credit Risk Dashboard

`GET /dashboard?tenantId={id}` — an HTML page, one row per counterparty with
outstanding sales invoices, sortable by clicking any column header:

| Column | Meaning |
|---|---|
| Contact | Name, links out to the contact record in Xero |
| Score | 0–100 credit score + A–F grade (see below); click to expand the breakdown |
| Outstanding | Total unpaid `AUTHORISED` invoice value |
| Concentration | This contact's share of total outstanding receivables across all contacts |
| Overdue | Portion of Outstanding that's past its due date |
| Oldest Overdue (days) | Age of the single oldest overdue invoice |
| Risk | `Current` / `Low` / `Medium` / `High`, derived from oldest-overdue-days (≥60 → High, ≥30 → Medium, else Low, none overdue → Current), escalated to High if Companies House shows the company as distressed |
| Payment Trend | Average days late on paid invoices, plus a trend arrow (comparing the second half of payment history against the first half) |
| Recommended Limit | Suggested credit limit; click to expand the breakdown |
| Companies House | Company status, incorporation age, insolvency history, registered charges, overdue filings |

An **Early Warnings** banner above the table lists issues not yet reflected in
a contact's risk tier — a reliable payer's first late payment, accelerating
lateness, exceeding their recommended limit, Companies House distress
signals, prior insolvency history, or concentration risk. It's collapsible
(defaults collapsed, state persisted per browser session) and can be grouped
either by counterparty or by warning type.

### Credit Score

`GET /api/xero/credit-risk/score?tenantId={id}`

A single 0–100 number (higher = safer) with an A–F grade, built by applying
deductions/bonuses to a baseline of 100:

| Factor | Effect |
|---|---|
| Baseline | +100 |
| Risk tier | High −40, Medium −25, Low −10, Current 0 |
| Payment trend | Worsening >7 days −15, worsening 2–7 days −7, improving <−2 days +5 |
| Concentration | >25% of receivables −10, >10% −5 |
| Companies House: distressed status | −30 |
| Companies House: overdue statutory filings | −10 |
| Companies House: prior insolvency history | −10 |

Result is clamped to 0–100. Grade: A ≥80, B ≥60, C ≥40, D ≥20, F <20. Each
contact's score cell expands (click) to show exactly which of the above
factors applied and by how much.

### Recommended Credit Limit

`GET /api/xero/credit-risk/limit-recommendations?tenantId={id}`

Xero has no native credit limit field, so this is a computed suggestion only
(never written back to Xero):

```
multiplier = (credit score / 100) × 3.0
recommended limit = ceil(average sales invoice amount × multiplier / 100) × 100
```

A perfect score of 100 caps the limit at 3× the contact's average invoice
size; a score of 0 recommends no further credit. The limit cell expands to
show the average invoice amount, the score-derived multiplier, and the
rounding. A contact currently owing more than their recommended limit is
flagged with ⚠ and surfaced as an Early Warning.

### Syncing risk tiers back to Xero

`POST /api/xero/credit-risk/sync?tenantId={id}` — writes each contact's
current risk tier back to Xero as Contact Group membership (`Risk: High` /
`Risk: Medium` / `Risk: Low` / `Risk: Current`), visible natively in Xero's
Contacts → Groups UI.

## Companies House enrichment

Contacts with a `CompanyNumber` set in Xero are looked up against the
[Companies House Public Data API](https://developer.company-information.service.gov.uk/)
for status, incorporation date, whether statutory filings (accounts /
confirmation statement) are overdue, and whether the company has ever been
through insolvency proceedings or has registered charges. A company is
treated as "distressed" if its status is dissolved, in liquidation,
receivership, administration, a voluntary arrangement, or insolvency
proceedings, or has converted/closed.

## Real-time updates

- `POST /webhooks/xero` — receives Xero's webhook notifications for invoice
  and contact changes. Every request's HMAC-SHA256 signature (`x-xero-signature`
  header, keyed with `Xero:WebhookKey`) is validated before anything is
  trusted; an invalid signature returns 401 (required for Xero's "Intent to
  Receive" verification check).
- `GET /dashboard/events` — a Server-Sent Events stream. When a webhook
  resolves to one or more affected contact IDs, connected dashboard tabs
  receive the list and reload themselves, briefly highlighting the affected
  rows.

Requires a public HTTPS URL for Xero to deliver webhooks to (a tunnel such as
`devtunnel` in local development).

## Dev tooling

`/dev` (`DevController`) is demo/testing-only, not part of the real feature
set:

- `GET /dev?tenantId={id}` — a grid of every counterparty with an editable
  "invoices to seed" count.
- `POST /dev/seed-invoices-bulk` — creates the requested number of test sales
  invoices per contact, with due dates spread across a fixed offset pattern
  (`-75, -45, -20, -5, 10, 25` days) so seeded data spans every risk tier.
- `POST /dev/populate-company-numbers` — assigns a Companies House number to
  every contact (except a small preserved set kept as intentional problem
  cases) from a pool of verified, clean, active companies.
- `POST /dev/set-company-number` — one-off override of a single contact's
  company number.

## Configuration

See `appsettings.json` / user-secrets for `Xero:ClientId`, `Xero:ClientSecret`,
`Xero:CallbackUri`, `Xero:Scope`, `Xero:WebhookKey`, and
`CompaniesHouse:ApiKey`. See `CLAUDE.md` for the full stack, project
structure, and setup instructions.
