## Status and Improvement Strategy

*Summary of current functionality and a forward strategy, synthesised from
this project's working session — including the pre-existing strategy
research in [Market Research](#expanding-beyond-companies-house) that
already had a plan in motion before the Creditsafe/competitor research
that followed.*

### Current PoC Functionality

**Core product** — see [Appendix B: Features](#appendix-b-features) for full detail:

- Xero OAuth 2.0 connection, one org at a time
- Dashboard: sortable table of every counterparty with outstanding invoices
  — 0–100 credit score (A–F grade) with click-to-expand reasoning,
  outstanding/overdue amounts, concentration %, oldest-overdue days, risk
  tier, payment trend, recommended credit limit, Companies House status
- Early Warnings banner — first late payment, accelerating lateness,
  over-limit, Companies House distress, prior insolvency, concentration risk
- Companies House enrichment — status, incorporation age, insolvency
  history, registered charges, overdue filings
- Real-time updates — Xero webhooks + Server-Sent Events push dashboard
  refreshes without polling
- Syncs risk tiers back into Xero as Contact Groups
- Dev/seed tooling for demo data (`/dev`)
- Landing page linking Dashboard, Dev tool, and rendered docs

**Infrastructure:**

- Deployed on Azure App Service (Basic B1, Always On) —
  `xero-extension-dev.azurewebsites.net`
- Secrets in Key Vault; Xero tokens persisted in Blob Storage (survives
  restarts — previously reset every deploy)
- Entra ID login gate — currently restricted to a single approved account;
  a second user's access is still pending, deferred by choice
- Webhook endpoint correctly excluded from the login gate (self-authenticates
  via HMAC signature, since it's a server-to-server call from Xero)

### Strategy for Improving It

**1. Close the "action layer" gap (biggest finding from competitor research)**

Every real competitor ([Chaser, Satago, Paidnice](#competitor-landscape))
bundles some action — automated chasing, reminders, payment plans — not
just risk visibility. This PoC is purely observational. This is the
single highest-leverage gap, but also the biggest scope decision: it turns
this from an internal tool into direct competition with Chaser/Satago.

**2. Add a third-party credit score — reconcile with the existing sequencing**

[Expanding Beyond Companies House](#expanding-beyond-companies-house)
recommends **Global Database first** (registry + credit score bundled,
cheaper, more developer-friendly), with **Creditsafe as a secondary/premium
source** for flagged high-risk counterparties only — not the other way
round. Worth reconciling that existing plan with the Creditsafe-specific
research in [Creditsafe API Research](#creditsafe-api-research) and
[Why Creditsafe](#why-creditsafe) before committing to either.

**3. International expansion is already scoped, gated on real pricing**

UK is solved (Companies House). US/Europe/Asia need a commercial provider
since no free registry covers them consistently. Next concrete step per
that strategy: get real quotes from Global Database and Creditsafe — desk
research can't finish this alone.

**4. Multi-platform expansion (QuickBooks next, then Sage)**

[Accounting Platform API Cost Analysis](#accounting-platform-api-cost-analysis)
flags QuickBooks' metered-reads pricing (free to 500K reads/month, up to
$4,500/mo beyond) and Sage's tight rate limits (2,500 req/day, no webhooks
— polling only) as real engineering constraints, not just licensing costs.
QuickBooks is prioritised next specifically because US private-company
data coverage is the weakest point worth stress-testing early.

**5. Near-term, smaller items**

- Decide on additional user access — still pending
- Multi-tenancy: today's `UserId`/token-store model assumes one shared Xero
  org; real multi-user/multi-org use would need the token store keyed per
  authenticated user, not a fixed constant
- Cost discipline principle from the strategy doc — cache/dedupe
  counterparty enrichment lookups from day one, since every provider
  considered uses metered/credit-based pricing

---

## Market Research

### Competitor Landscape

Desk research on Xero-integrated apps that overlap with this app's credit-risk
monitoring space, plus a gap analysis against the current feature set (see
[Appendix B: Features](#appendix-b-features)).

#### The Landscape

| App | Data Source(s) | Focus | Pricing (entry → top published tier) | Closest to this app? |
|---|---|---|---|---|
| **Chaser** | Companies House risk monitoring | Credit risk tracking + debt collection workflow, targets mid-market finance teams | £199/mo (<£4M revenue, 4 users) → £899/mo (<£100M revenue); custom above that. +10% saving if paid yearly | **Very close** — same data source (Companies House) already in use here |
| **Satago** | Experian credit scoring | Upfront credit checks + ongoing chasing + invoice finance | £45/mo (Basic, 25 credit reports, 100 emails) → £200/mo (Platinum, unlimited reports*, dedicated account manager) | **Very close** — the Experian-flavoured version of what Creditsafe integration would add |
| **Paidnice** | Bank of England base rate (statutory interest calc) | Automated late-fee/interest enforcement, multi-channel chasing | $69/mo (Essentials, 150 invoices, unlimited team members) → $999+/mo (Custom, 4,000+ invoices) | Less close — no external credit-risk data, more about enforcement than risk scoring |
| **Credit Hound** | None (internal task data only) | CRM-style debtor worklist, Sage-oriented | Conflicting figures found: ~$70/mo per one aggregator vs. £150/mo (~$200) per Sage Intacct's own marketplace listing — genuinely unclear, varies by accounting-system integration and edition | Not close — no credit scoring at all |
| **Kolleno** | "AI agents" across accounting/ERP/CRM systems | Full AR lifecycle incl. e-invoicing/reconciliation | ~£650/user/mo (entry) → ~£1,245/user/mo; custom above that (aggregator-sourced, not confirmed direct) | Adjacent — broader platform play, not credit-risk-focused |
| **Upflow** | Revenue-based analysis, internal risk scoring | AR analytics (DSO trends, cohort analysis), forecasting | Free (Discover, analytics only) → ~$440/mo (Grow) → ~$880/mo (Scale); custom above $50M ARR (aggregator-sourced, not confirmed direct) | Adjacent — analytics/forecasting angle rather than third-party credit data |

\* Satago's "unlimited" credit reports are subject to fair usage.

**Confidence note:** Chaser, Satago, and Paidnice figures are pulled directly
from their own pricing pages. Credit Hound, Kolleno, and Upflow figures come
from third-party aggregators and carry more uncertainty — treat as
directional, not quotable.

#### What This App Already Does Better

- A genuinely computed 0–100 credit score derived from the org's **own** Xero
  payment history (not third-party) — see [Appendix B: Features § Credit Score](#credit-score)
- Companies House enrichment (status, insolvency history, overdue filings,
  registered charges)
- Real-time updates via Xero webhooks + Server-Sent Events — dashboard rows
  update live as invoices/contacts change
- Syncs risk tiers back into Xero as Contact Groups

None of the apps above combine own-data scoring + Companies House enrichment
+ real-time updates in quite the same way.

#### The Gap: No Action Layer

The clearest gap is that this app is purely **observational** — it surfaces
who's risky but takes no action. Every competitor above bundles at least
basic action (automated chasing, payment reminders, collection workflows),
because Xero users shopping in this category are usually trying to *reduce*
bad debt, not just see it coming.

**Secondary, smaller gaps:**
- No third-party-validated credit score (the Creditsafe evaluation — see
  [Creditsafe API Research](#creditsafe-api-research) — would close this)
- No proactive customer-facing communication at all

**Tradeoff on closing the main gap:** adding a chasing/collections layer
(automated reminders, payment plans) turns this from an internal analytics
tool into direct competition with Chaser and Satago on their own turf — a
much bigger scope and a different kind of product (customer-facing comms,
not just internal reporting). Worth deciding deliberately rather than
drifting into it feature-by-feature.

**Sources:** [Credit Control Software for Xero: How to Choose the Right Tool in 2026 (UK Guide)](https://accounting.events/reviews/credit-control-software-guide/) · [Debtor Management Apps — Xero App Store](https://apps.xero.com/us/function/debtor-tracking) · [Top Rated Accounts Receivable Software with Xero 2026 | GetApp](https://www.getapp.com/finance-accounting-software/accounts-receivable/w/xero/) · [Chaser Pricing (official)](https://www.chaserhq.com/pricing) · [Satago Pricing (official)](https://www.satago.com/pricing/) · [Paidnice Pricing (official)](https://www.paidnice.com/pricing) · [Credit control software compared: Chaser, Kolleno and Upflow (2026) | Trove](https://trove.works/credit-control-software-pricing/) · [Credit Hound — Sage Intacct Marketplace listing](https://marketplace.intacct.com/MPListing?lid=a2D0H00000DtLy1UAF)

### Creditsafe API Research

#### What Creditsafe Is

A business credit reference agency (UK/international) — similar in purpose to
Companies House's data, but instead of just filing/insolvency status, they
provide an actual computed **credit score and recommended credit limit** for
companies, based on their own risk models, plus directors' info, balance
sheet data, and negative information (CCJs, etc.).

#### The Connect API

**Base URLs**

| Environment | URL |
|---|---|
| Production | `https://connect.creditsafe.com/v1` |
| Sandbox | `https://connect.sandbox.creditsafe.com/v1` |

**Authentication**

- `POST /authenticate` with `username` + `password` in the body
- Returns a Bearer JWT, valid for 1 hour (re-authenticate to get a fresh one)
- Used as `Authorization: Bearer <token>` on all subsequent calls
- Rate limits: max 5 identical invalid requests per 2 minutes (429), and a
  lockout after 10,000 requests in 5 minutes

**Key Endpoints**

| Endpoint | Purpose |
|---|---|
| `GET /companies?countries=GB&regNo=...` | Search/look up a company (by UK company number, name, etc.) — returns a `connectId` |
| `GET /companies/{connectId}` | Full credit report: credit score, credit limit, directors, balance sheet, negative info. Supports `Accept: application/json+pdf` for a Base64 PDF report too |

#### International Coverage

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

#### Fit With This App

Maps cleanly onto the existing `ICompaniesHouseService`/`CompaniesHouseService`
pattern — a `CreditsafeService` alongside it, same shape (look up by company
number → get a profile). It would give a genuine third-party credit score,
versus:

- The current `CreditLimitRecommendation` — self-calculated purely from this
  org's own Xero invoice history
- Companies House data — just filing/insolvency status, not a score

#### Key Considerations

- **It's a paid commercial product**, not free/public like Companies House —
  requires an actual account/contract with Creditsafe. Signup looks
  sales/demo-led rather than instant self-serve:
  [creditsafe.com/us/en/enterprise/integrations/company-data-api.html](https://www.creditsafe.com/us/en/enterprise/integrations/company-data-api.html)
- Credentials would need Key Vault storage, same as the Xero secrets
- Worth deciding upfront: does this **replace** the current recommended-limit
  logic, or sit **alongside** it as an extra column/data point?

**Sources:** [doc.creditsafe.com](https://doc.creditsafe.com/) · [Company Credit Report docs](https://doc.creditsafe.com/connect-apis-catalog/product-catalog/creditrisk/creditandrisk/companies/companycreditreport) · [connect-docs OpenAPI spec (archived)](https://github.com/creditsafe/connect-docs) · [Creditsafe Data](https://www.creditsafe.com/us/en/more/about/our-data.html) · [International Company Credit Reports & Credit Scores](https://www.creditsafe.com/us/en/credit-risk/credit-reports/international-credit-reports.html) · [Business Credit Monitoring & International Company Monitoring](https://www.creditsafe.com/us/en/credit-risk/credit-reports/company-monitoring.html)

### Why Creditsafe

Why Creditsafe over Dun & Bradstreet or Experian Business, specifically:

1. **Only one with concrete, usable technical documentation.** Creditsafe has
   a public OpenAPI spec with clear auth (`POST /authenticate` → Bearer JWT)
   and endpoints (`GET /companies`, `GET /companies/{connectId}`), plus a
   public sandbox. D&B's and Experian's developer portals are harder to
   evaluate from the outside — D&B's public docs surfaced mostly Nordic
   products rather than their core global API, and neither offers a fully
   self-serve path to production access.

2. **Purpose-built for this exact lookup.** `GET /companies?countries=GB&regNo=...`
   → `GET /companies/{connectId}` maps directly onto "look up a UK company by
   registration number, get a credit score and recommended limit" — the same
   shape as the existing `CompaniesHouseService` in this app.

3. **Likely the cheaper option, though unverified.** None of the three
   publish real pricing (all are quote-based enterprise sales). Third-party
   estimates put D&B at $10k–$50k+/yr and Experian's UK business report
   subscriptions at $450–$750/yr for a small volume tier — both point to
   Creditsafe being priced for API-driven use rather than occasional report
   lookups, but this needs confirming directly with sales before committing.

4. **Right-sized for the use case.** D&B and Experian are broader, more
   established enterprise data providers with correspondingly heavier sales
   processes and (likely) minimum contract sizes suited to larger
   organisations. Creditsafe's product is more narrowly scoped to company
   credit reports, which matches what this app actually needs.

5. **Not UK-limited, so it scales with the app.** The `countries` parameter
   on `GET /companies` makes multi-country lookup first-class, with reported
   coverage of 100+ countries direct and 200+ via international reports —
   future-proofs the integration if overseas counterparties ever need
   checking, without switching providers later. D&B and Experian are also
   global (arguably more established internationally), so this isn't a
   unique advantage — just confirmation Creditsafe isn't a UK-only trade-off.

**Caveat:** this is a lean, desk-research comparison — not a vendor
evaluation. Before committing, get real pricing and a sandbox trial from
Creditsafe, and at least a pricing conversation with D&B/Experian, to
confirm points 1 and 3 hold up under direct contact.

### Expanding Beyond Companies House

*Strategy for moving from UK-only (Companies House) to US, European, and
Asian customer coverage.*

#### The Problem

Companies House only covers UK-registered entities. As the credit risk
extension expands to US, European, and Asian Xero/QuickBooks/Sage
customers, counterparty enrichment, tiering, and payment-behaviour scoring
need a data source (or sources) that work outside the UK. Companies House
itself has no equivalent reach — it only provides what companies are
legally required to file with the UK registry, with no credit scores,
revenue estimates, or cross-border data.

#### The Regional Gap

| Region | Free/government source | Coverage gap |
|---|---|---|
| **UK** | Companies House (existing) | None — solved |
| **US** | SEC EDGAR | Free, but public companies only. No unified private-company registry — records are fragmented across 50 state Secretary of State offices with inconsistent API access |
| **Europe** | National registries (Handelsregister, INPI/RNE, etc.) | Exist per-country but access quality and API maturity vary widely |
| **Asia** | Jurisdiction-by-jurisdiction | Patchiest coverage; rarely has clean developer-friendly APIs |

**Conclusion:** no free government source will give consistent,
credit-relevant coverage across all three regions. A commercial aggregator
or credit bureau is needed to fill the gap left by Companies House.

#### Provider Options Evaluated

**Registry aggregators (entity verification, not credit scoring):**
- **OpenCorporates** — largest open database, but capped at 500 API
  calls/month, roughly half its sources no longer updating, no beneficial
  ownership data. Fine for prototyping only.
- **North Data** — strong for European/German registry data specifically.
- **Zephira** — API-first, developer-friendly, transparent pricing from
  $99/month (Starter), covering 300M+ companies across 150+ countries,
  pulling directly from government registries with AI-based
  cross-jurisdiction normalization. Best pure-registry option if deep
  credit scoring isn't required.

**Credit/risk-focused providers (closer fit for this use case):**
- **Creditsafe** — an actual credit bureau, not just a registry aggregator.
  Provides credit scores and payment behaviour data directly, which is what
  the existing counterparty tiering logic needs. Pricing is not published;
  requires direct quote. Third-party pricing trackers describe a tiered
  model (Standard/Plus/Premier) with no permanent free tier, though
  reviewers frequently cite it as notably cheaper than Dun & Bradstreet and
  Equifax, with a flat-rate model that avoids per-report fee creep.
- **Global Database** — combines registry data (sourced from Companies
  House, public filings, stock exchanges, proprietary web crawling) with
  credit scores/limits and contact enrichment in a single API. 400M+
  company profiles, 480M+ contacts. Priced via a credit-based subscription
  model (buy a pool of credits, consumption varies by endpoint). Broader
  than Creditsafe in scope — could reduce the need for a separate registry
  + credit bureau integration.
- **Sayari / Moody's Orbis** — go deepest on beneficial ownership and
  complex risk structures, but oriented more toward compliance/KYB than
  SME credit scoring. Likely overkill and over-cost for this use case
  unless customers include higher-risk jurisdictions.

#### Recommended Approach

**Phase 1 — Get real pricing.** Neither Creditsafe nor Global Database
publish API pricing. The next concrete step is requesting quotes/API
documentation directly from both, since the strategy below can't be
finalised on desk research alone.

**Phase 2 — Pilot with Global Database first.** It's the more
developer-friendly, API-first option, and — critically — bundles registry
verification *and* credit signal in one integration. This avoids
maintaining two separate data relationships (a registry aggregator plus a
separate credit bureau) during early expansion, which matters given the
credit risk extension is still a one-person build.

**Phase 3 — Evaluate Creditsafe as a secondary or premium-tier source.** If
Global Database's credit scoring proves too thin for higher-stakes tiering
decisions, add Creditsafe as a secondary source for flagged/high-risk
counterparties only — keeping most volume on the cheaper aggregator and
reserving bureau-grade reports for cases that need them. This mirrors how
Creditsafe's own tiers are structured (limited "Fresh Investigations" at
the top tier), suggesting even Creditsafe expects premium reports to be
used selectively rather than for full-portfolio coverage.

**Phase 4 — Regional sequencing.** Given QuickBooks (US) is the next
priority integration after Xero, prioritise validating US private-company
coverage first — this is the weakest point for both providers and the area
most worth stress-testing before committing. European coverage is likely
to be stronger out of the gate given both providers' registry-sourced
European data; Asian coverage should be treated as a later-stage addition
once volume justifies it.

#### Cost Discipline

Given the metered/credit-based pricing models across every option here
(mirroring the QuickBooks reads-metering issue flagged in
[Accounting Platform API Cost Analysis](#accounting-platform-api-cost-analysis)),
the enrichment cost per counterparty lookup should be modelled against
expected customer volume *before* committing to a provider — the same
discipline applied to the Xero/Sage/QuickBooks API cost analysis. A
credit-based model in particular rewards caching and deduplication (e.g.
not re-enriching the same counterparty across multiple customer accounts),
which should be a design principle from day one rather than a later
optimisation.

#### Open Questions to Resolve via Provider Quotes

1. What does Global Database's credit-based pricing actually cost per
   lookup at expected volume (dozens to low hundreds of counterparties per
   customer)?
2. Does Creditsafe's API pricing scale down to a solo-developer/early-stage
   volume, or is it enterprise-only in practice?
3. How current is each provider's US private-company data, given the
   absence of a unified US registry?
4. Do either provider's terms restrict use of their data for training
   AI/ML models or building a resellable product (as Xero's new terms now
   do) — this needs checking before building deeper dependency on either.

### Accounting Platform API Cost Analysis

*Comparison of integration costs across Xero, Sage, and QuickBooks for the
Xero-based credit risk extension.*

#### Summary

| | Xero | Sage Business Cloud | QuickBooks Online |
|---|---|---|---|
| **Base cost** | Free (Starter tier) | Free | Writes free; reads metered |
| **Cost at small scale** | Free up to 5 connections | Free | Free up to 500K reads/month |
| **Cost at growth scale** | $35 AUD/mo (~£18) up to 50 connections | Free (capped by rate limits, not fees) | Paid tiers required beyond 500K reads/month, up to $4,500/mo |
| **Cost at large scale** | $245 AUD/mo up to 1,000 connections; $1,445 AUD/mo up to 10,000 | Free | Higher paid tier (Silver/Gold/Platinum), exact pricing not published |
| **Overage pricing** | $2.40 AUD/GB of data egress beyond tier allowance | N/A (hard rate-limited instead) | N/A (tiered reads model) |

#### Xero — Detailed Tier Breakdown

Pricing took effect 2 March 2026, replacing the previous free/revenue-share
model.

| Tier | Monthly fee | Max connections | Rate limit | Egress allowance |
|---|---|---|---|---|
| Starter | Free | 5 | 1,000 calls/day/org | n/a |
| Core | $35 AUD | 50 | 5,000 calls/day/org | 10 GB |
| Plus | $245 AUD | 1,000 | 5,000 calls/day/org | 50 GB |
| Advanced | $1,445 AUD | 10,000 | 5,000 calls/day/org | 250 GB |
| Enterprise | Custom | Unlimited | 5,000 calls/day/org | Custom |

**Key gotchas:**
- The Journal endpoint (raw journal entries) is locked behind the Advanced
  tier and requires a security assessment — only relevant if the credit
  scoring logic needs journal-level data rather than just
  invoices/payments.
- Existing apps were migrated to this model with 30 days' notice; new apps
  go straight onto it.

#### Sage Business Cloud Accounting

- **API access is free**, but constrained by tight limits: ~2,500
  requests/day per company, 100/minute.
- No webhooks — polling only, using `updated_from`/`updated_to` filters.
- Main cost is engineering time (5-minute access tokens, manual
  re-authorization if refresh tokens lapse after 31 days), not licensing
  fees.
- Distribution requires Sage partner certification and Global Marketplace
  listing.

#### QuickBooks Online

- **Writing data remains free.** **Reading data is metered** under a
  tiered model introduced in 2025:
  - Free "Builder" tier: 500,000 reads/month
  - Paid tiers (Silver, Gold, Platinum): required beyond that, with
    published pricing not fully disclosed in available sources
  - Overall App Partner Program cost range cited elsewhere: $0–$4,500/month
    depending on scale
- Rate limit is generous relative to Sage: 500 requests/min per company, 10
  concurrent.
- For a credit-risk product continuously polling invoices/payments across
  many customer companies, this metered-read model is the one most likely
  to generate a real, ongoing cost line as usage grows — worth modelling
  against expected customer count before committing engineering time.

#### Bottom Line for the Credit Risk Extension

- **Xero** is cheap through early growth — free to 5 customers, ~£18/month
  to 50 — and scales predictably and transparently after that.
- **Sage** has no direct licensing cost, but its low rate limits and lack
  of webhooks make it more of an engineering cost than a fee cost.
- **QuickBooks** is the most commercially important target for US
  penetration, but is also the platform where read costs need the most
  careful forecasting before scaling up.

---

## Appendix A: Overview

A tool that helps you spot which customers are becoming a credit risk before
it turns into a bad debt — using the invoice data already in Xero, enriched
with UK company registry information, kept up to date automatically.

### Connecting your Xero account

A one-time sign-in through Xero's own secure login screen. Once connected,
the dashboard reads your sales invoices and customer records directly — no
data entry required.

### The Credit Risk Dashboard

A single table showing every customer with money currently owed, sortable
by clicking any column:

| Column | What it tells you |
|---|---|
| Customer | Name, with a link straight through to their record in Xero |
| Credit Score | An overall 0–100 score summarising how risky this customer is, with a simple letter grade (A best, F worst) — click to see why they got that score |
| Outstanding | Total amount currently owed |
| Concentration | How much of your total money owed sits with this one customer — too much riding on one customer is a risk in itself |
| Overdue | How much of what's owed is already past its due date |
| Oldest Overdue | How many days the oldest unpaid invoice has been overdue |
| Risk Level | Current, Low, Medium, or High, based on how overdue their invoices are |
| Payment Trend | Whether this customer has been paying faster or slower recently compared to their own history |
| Recommended Limit | A suggested cap on how much credit to extend to this customer — click to see how it was worked out |
| Company Status | Whether the company is still actively trading, whether they're behind on their own statutory filings, and whether they've ever been through insolvency |

### Early Warnings

A highlighted panel above the table flags customers showing early signs of
trouble — before it's serious enough to show up in their overall risk
rating. This includes a reliably-paying customer's first ever late payment,
a customer whose payments are getting slower at an accelerating rate, a
customer who has gone over their recommended credit limit, or worrying
information from the companies register. Warnings can be grouped either by
customer or by type of warning, and the panel can be collapsed once
reviewed.

### How the Credit Score works

Every customer starts from a clean slate and points are deducted (or
occasionally added back) based on:

- How overdue their invoices currently are
- Whether their payment behaviour is getting better or worse over time
- How much of your total exposure is concentrated in this one customer
- Whether the companies register shows any signs of financial distress,
  overdue filings, or a history of insolvency

The result is a single number that's easy to compare across your whole
customer list, with a plain-language breakdown available for every
customer explaining exactly which of the above applied to them.

### How the Recommended Credit Limit works

The suggested limit is based on how much this customer typically orders,
scaled up or down according to their Credit Score — a customer with a
strong score can responsibly be offered a higher multiple of their typical
order value, while a customer with a poor score is offered little to no
further credit. If a customer already owes more than their recommended
limit, that's flagged directly on the dashboard and raised as an Early
Warning.

### Company background checks

Where a customer's UK company registration number is on file, their record
is automatically checked against the public companies register for their
current trading status, how long they've been established, whether they're
behind on their own accounts or confirmation statement filings, whether
they have any registered charges (security given to a lender) against them,
and whether they've ever previously been through insolvency — even if
they're trading normally today.

### Always up to date

The dashboard refreshes itself automatically the moment something relevant
changes in Xero — a new invoice, a payment, a change to a customer record —
with no need to manually reload the page.

---

## Appendix B: Features

A credit-risk monitoring dashboard built on top of Xero. It connects to a Xero
organisation via OAuth 2.0, reads sales invoice and contact data, and turns it
into a live-updating risk dashboard — enriched with UK company data from
Companies House and pushed in real time via Xero webhooks.

### Connecting to Xero

1. `GET /auth/xero/connect` — redirects to Xero's login/consent screen.
2. Xero redirects back to `GET /auth/xero/callback?code=...`, which exchanges
   the code for tokens and stores them (in-memory; resets on app restart).
3. `DELETE /auth/xero/disconnect` — clears the stored tokens.

Tokens are held against a single hardcoded `default-user` — see
[Appendix C: Architecture § Where to extend](#appendix-c-architecture) and
`CLAUDE.md`'s Key Extension Points section (repo root) for what a
multi-user deployment would need to change.

### Credit Risk Dashboard

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

#### Credit Score

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

#### Recommended Credit Limit

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

#### Syncing risk tiers back to Xero

`POST /api/xero/credit-risk/sync?tenantId={id}` — writes each contact's
current risk tier back to Xero as Contact Group membership (`Risk: High` /
`Risk: Medium` / `Risk: Low` / `Risk: Current`), visible natively in Xero's
Contacts → Groups UI.

### Companies House enrichment

Contacts with a `CompanyNumber` set in Xero are looked up against the
[Companies House Public Data API](https://developer.company-information.service.gov.uk/)
for status, incorporation date, whether statutory filings (accounts /
confirmation statement) are overdue, and whether the company has ever been
through insolvency proceedings or has registered charges. A company is
treated as "distressed" if its status is dissolved, in liquidation,
receivership, administration, a voluntary arrangement, or insolvency
proceedings, or has converted/closed.

### Real-time updates

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

### Dev tooling

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

### Configuration

See `appsettings.json` / user-secrets for `Xero:ClientId`, `Xero:ClientSecret`,
`Xero:CallbackUri`, `Xero:Scope`, `Xero:WebhookKey`, and
`CompaniesHouse:ApiKey`. See `CLAUDE.md` (repo root) for the full stack,
project structure, and setup instructions.

---

## Appendix C: Architecture

Technical reference for how `XeroExtension.Web` is put together. See
[Appendix B: Features](#appendix-b-features) for what it does and
`CLAUDE.md` (repo root) for stack/setup basics.

### Stack

- .NET 10 / ASP.NET Core Web API, controller-based (no MVC views — every
  HTML response is a hand-built string returned as `ContentResult`)
- `Xero.NetStandard.OAuth2` SDK (v14+) for OAuth 2.0 and the Accounting API
- `Microsoft.Extensions.Caching.Memory` for the token store and nothing else
  persistent — there is no database in this project

### Project layout

```
src/XeroExtension.Web/
  Controllers/    HTTP endpoints — thin, delegate to Services
  Services/       All business logic and external API calls
  Models/         Plain data classes passed between Services and Controllers
  Program.cs      DI container wiring, middleware pipeline
```

Standard three-layer separation: Controllers never call Xero or Companies
House directly, and Services never touch `HttpContext` or build HTML.

### Dependency injection (`Program.cs`)

| Service | Lifetime | Why |
|---|---|---|
| `ITokenStore` → `InMemoryTokenStore` | Singleton | Backed by `IMemoryCache`; needs to persist across requests within the process |
| `IXeroClient` (SDK) | via `AddHttpClient` | One `HttpClient` per client type, standard pattern |
| `IXeroService` → `XeroService` | Scoped | Talks to Xero using the current request's token |
| `ICreditRiskService` → `CreditRiskService` | Scoped | See caching below — the scoping is load-bearing, not incidental |
| `ICompaniesHouseService` → `CompaniesHouseService` | via `AddHttpClient` | `HttpClient` pre-configured with Basic-auth header built from `CompaniesHouse:ApiKey` |
| `DashboardNotifier` | Singleton | Must be shared across all requests/connections to fan out webhook events to every open SSE connection |

`AddJsonOptions` registers a global `JsonStringEnumConverter` so every enum
(`CreditRiskLevel`, `EarlyWarningType`) serialises as its name, not a number.

A single `app.UseExceptionHandler(...)` middleware maps `NotConnectedException`
to HTTP 401 and anything else to a generic 500 — this is the only
centralised error handling in the app; controllers don't try/catch.

> **Note:** as of this session's Azure work, the token store is
> conditionally `BlobTokenStore` (Azure Blob Storage-backed) when a
> `Storage:BlobServiceUri` setting is present, falling back to
> `InMemoryTokenStore` for local dev. See
> [Status and Improvement Strategy](#status-and-improvement-strategy) for
> the deployed state. This document otherwise reflects the original
> `ARCHITECTURE.md` as written.

### Authentication & token lifecycle

- `AuthController` — three endpoints (`connect`, `callback`, `disconnect`)
  that drive the OAuth 2.0 Authorization Code flow using the SDK's
  `IXeroClient`.
- Tokens are stored via `ITokenStore` keyed by a **hardcoded `UserId`
  constant** (`"default-user"`) — this is a single-tenant/single-user demo
  design. A real multi-user deployment replaces every `UserId` constant
  (there's one each in `AuthController` and `XeroService`) with a value
  derived from `HttpContext.User` claims, and swaps `InMemoryTokenStore` for
  a persistent, per-user store.
- `InMemoryTokenStore` wraps `IMemoryCache` with a 24-hour absolute
  expiration — but in practice tokens vanish immediately whenever the app
  process restarts (`dotnet run` during development), which is why
  reconnecting via `/auth/xero/connect` is needed after every restart.
- Token refresh is handled transparently: `XeroService.GetValidTokenAsync()`
  is the single choke point every API call goes through. It checks
  `XeroTokenSet.IsExpired` (a 1-minute safety margin before the real
  expiry) and, if expired, calls the SDK's refresh-token flow and writes
  the new token set back to the store before returning it. Callers never
  see or handle expiry themselves.

### Xero SDK integration notes

- All Accounting API calls go through a single `AccountingApi` instance
  held by `XeroService`.
- The SDK's generated response wrapper types expose their collections
  through **underscore-prefixed properties** — `_Invoices`, `_Contacts`,
  `_Accounts`, `_ContactGroups` — an artefact of the swagger-codegen
  generator, not a typo. Every call site null-coalesces to `[]` since the
  SDK returns `null` collections rather than empty ones on some responses.
- `XeroService` also does a few things beyond plain passthroughs:
  - `EnsureContactGroupAsync` / `ReplaceContactGroupMembersAsync` —
    find-or-create a named Contact Group, then wholesale-replace its
    membership (delete all existing group-contact links, then re-add).
    Used to sync computed risk tiers back into Xero as native groups.
  - `GetInvoiceContactIdAsync` — looks up a single invoice by ID to resolve
    which contact a webhook INVOICE event affects.
  - `CreateSalesInvoiceAsync` / `SetContactCompanyNumberAsync` — write
    operations used only by the dev/demo seeding tooling, not the real
    feature set.

### The credit risk domain layer (`CreditRiskService`)

This is where almost all of the actual logic lives. It's registered
**Scoped**, meaning one instance per HTTP request — and that's
deliberate: several of its public methods depend on each other, and
without request-scoped caching each dependent call would independently
re-fetch invoices and re-run the (rate-limited, network-bound) Companies
House lookups from scratch.

```
private List<Invoice>?            _cachedInvoices;
private List<ContactCreditRisk>?  _cachedRisk;
private List<ContactPaymentTrend>? _cachedTrend;
```

`GetInvoicesCachedAsync` and the `_cachedRisk`/`_cachedTrend` null-checks
at the top of `GetContactRiskAsync` / `GetPaymentTrendAsync` are a simple
memoize-once-per-request pattern — safe only because the service itself
doesn't outlive one request.

#### Method dependency graph

```
GetContactRiskAsync            (invoices, contacts, Companies House)
        │
        ├──> GetPaymentTrendAsync        (invoices only)
        │
        ├──> GetCreditScoresAsync        (risk + trend)
        │         │
        │         └──> GetCreditLimitRecommendationsAsync  (invoices + risk + score)
        │                       │
        └───────────────────────┴──> GetEarlyWarningsAsync (risk + trend + recommendations)
```

This shape matters: `GetCreditScoresAsync` deliberately does **not** depend
on `GetCreditLimitRecommendationsAsync` (it used to, via an "exceeds
recommended limit" score deduction, which was removed once the limit
itself became score-driven — that would otherwise have created a circular
dependency: limit needs score, score needs limit).

#### `GetContactRiskAsync`

1. Filters invoices to outstanding `ACCREC` (sales) invoices with
   `AmountDue > 0`, groups by contact.
2. For each group, computes `OldestOverdueDays` and buckets it into a
   `CreditRiskLevel` (`Current` if nothing's overdue, else `Low` <30 days,
   `Medium` <60, `High` ≥60).
3. Resolves each contact's `CompanyNumber` (if set in Xero) and fires off
   Companies House lookups for every **distinct** company number in
   parallel via `Task.WhenAll` — not one lookup per contact, since several
   contacts can share a company number pattern in demo data.
4. If Companies House reports the company as distressed, the risk level is
   force-escalated to `High` regardless of the invoice-age calculation.
5. Computes `ConcentrationPercent` per contact as a share of total
   outstanding across all at-risk contacts.

#### `GetPaymentTrendAsync`

Looks only at `PAID` invoices with both a due date and a paid date. Splits
each contact's chronological payment history in half and compares the
average lateness of the second half against the first — a positive
`TrendDelta` means payments are getting later, negative means earlier.
Contacts with fewer than 2 paid invoices get `TrendDelta = 0` (not enough
signal).

#### `GetCreditScoresAsync`

Starts every contact at a baseline of 100 and applies a fixed set of
deductions/bonuses (risk tier, payment trend, concentration, three
Companies House signals), clamps to 0–100, and buckets into an A–F grade.
Each deduction is recorded as a human-readable string in
`ContactCreditScore.Reasons`, in application order, which is what the
dashboard's score drilldown renders verbatim — the reasons list isn't a
separate explanation generated after the fact, it's produced by the same
`Apply(delta, label)` local function that mutates the running score, so it
can never drift out of sync with the number it explains.

#### `GetCreditLimitRecommendationsAsync`

Groups sales invoices (`AUTHORISED` or `PAID`) by contact, takes the
average invoice value, and scales it by `(score / 100) × 3.0` — a
continuous multiplier rather than the four discrete risk-tier multipliers
used before scoring existed. Rounds up to the nearest £100. Builds the
same kind of `Reasons` breakdown as the score, for its own drilldown.
Contacts with no outstanding invoices (so absent from the risk/score
lists) default to a score of 100, matching the "Current, no issues"
treatment used elsewhere.

#### `GetEarlyWarningsAsync`

Not a scoring model — a flat list of independently-triggered
`EarlyWarningTrigger` records, aggregated from risk, trend, and
recommendation data: first-ever late payment, accelerating lateness,
exceeding the recommended limit, Companies House distress/insolvency
signals, and concentration risk. Each warning carries a free-text
`Message` composed at trigger time.

#### `SyncRiskGroupsToXeroAsync`

The only method in this service that writes anything back to Xero. Maps
each `CreditRiskLevel` to a fixed group name (`Risk: High` etc.) and calls
`XeroService.EnsureContactGroupAsync` / `ReplaceContactGroupMembersAsync`
once per tier.

### Companies House integration

`CompaniesHouseService` wraps a single `HttpClient` (Basic-auth header
built once in `Program.cs` from `CompaniesHouse:ApiKey`, base address
`api.company-information.service.gov.uk`). Deliberately **fail-soft**:
any non-success status code or exception (network failure, malformed
JSON) is logged and returns `null`, never thrown — a Companies House
outage degrades the risk pipeline (less enrichment) rather than breaking
it. `CompanyProfile.HasInsolvencyHistory` / `HasCharges` are derived from
whether the API's `links.insolvency` / `links.charges` fields are
*present*, not from a boolean field — that's how the Companies House API
signals these facts.

### Real-time update pipeline

```
Xero organisation change
        │
        ▼
POST /webhooks/xero  (XeroWebhookController)
  - reads raw body, validates HMAC-SHA256 signature against Xero:WebhookKey
  - invalid signature → 401 (required for Xero's "Intent to Receive" check)
  - valid → parses events, resolves each to an affected contact ID
        │
        ▼
DashboardNotifier.NotifyChanged(contactIds)   (Singleton, in-process event)
        │
        ▼
GET /dashboard/events   (DashboardController, one SSE stream per browser tab)
  - awaits the Changed event via a TaskCompletionSource
  - sends `data: {"contactIds":[...]}\n\n`, or a keep-alive comment every 25s
        │
        ▼
Browser: EventSource.onmessage
  - stores contactIds + a 15s expiry in sessionStorage
  - location.reload()
        │
        ▼
Next page load: inline <script> reads sessionStorage, applies
.highlight-updated to the affected rows, clears itself after the
remaining time
```

Signature validation matters more than it looks: Xero's own webhook setup
flow deliberately sends one request with a bad signature and expects 401
back, to confirm the endpoint actually checks it rather than blindly
accepting everything.

Requires a public HTTPS URL for Xero to deliver to — a tunnel (e.g.
`devtunnel`) in local development, since Xero can't reach `localhost`.

### Dashboard rendering (`DashboardController`)

There's no view engine. `Index` builds the entire HTML document as one
big C# string and returns it via `Content(html, "text/html; charset=utf-8")`.
A few conventions worth knowing before touching this file:

- Raw string literals (`$"""..."""` / `$$"""..."""`) are used throughout.
  Blocks containing literal CSS braces need the double-`$` form
  (`$$"""..."""`), with interpolation holes written as `{{expr}}` instead
  of `{expr}` — mixing the two is a compile error (CS9006).
- Every sortable `<td>` carries a `data-sort-value` attribute holding the
  raw comparable value (decimal, int, or an ordinal), kept deliberately
  separate from the formatted display text inside the cell. Client-side
  JS sorts on that attribute, not on `textContent`.
- Drilldowns (Score, Recommended Limit) are plain `<details>/<summary>` —
  no JS needed for expand/collapse, browser-native.
- The Early Warnings panel's collapse state and the highlight-after-reload
  state both live in `sessionStorage`, not component state, because a
  webhook-triggered update does a full `location.reload()` rather than a
  partial DOM patch.
- Currency values are formatted as an explicit `£{value:N2}` rather than
  the culture-dependent `:C` specifier — `:C` was found to render the
  generic currency sign (`¤`) instead of `£` on Azure's Linux runtime,
  since it depends on server culture/globalization resolution that isn't
  reliable there.

### Dev/demo tooling (`DevController`)

Explicitly out-of-scope for the real feature set (see its doc comment).
Provides a UI and endpoints for seeding test invoices across a fixed due-
date offset pattern (so seeded data spans every risk tier by construction)
and for assigning Companies House company numbers to demo contacts from a
pool of verified real companies. Two contacts (`City Limousines`,
`Ridgeway University`) are hardcoded as permanent problem-scenario demo
cases; everything else in the bulk-populate pool is genuinely clean.

### Where to extend

- **Multi-tenancy**: replace the `UserId` constant in `AuthController` and
  `XeroService` with a value from `HttpContext.User` claims.
- **Persistent tokens**: implemented — see `BlobTokenStore` and the note
  under [Dependency injection](#dependency-injection-programcs) above.
- **New Xero data**: add methods to `IXeroService`/`XeroService` — any
  class under `Xero.NetStandard.OAuth2.Api` is available via the SDK.
- **New risk signals**: add a factor inside `GetCreditScoresAsync`'s
  `Apply(...)` calls — it automatically appears in every score's
  `Reasons` drilldown with no other changes needed.

---

## Reference

### Companies House API Reference

*Reference summary for the credit risk extension's UK counterparty
enrichment.*

#### Overview

- **Base URL:** `https://api.company-information.service.gov.uk`
- **Auth:** HTTP Basic, API key as username, no password required
- **Format:** JSON, read-only (no filing/updating via API)
- **Rate limit:** 600 requests per 5 minutes per key (429 + `Retry-After`
  header if exceeded)
- **Cost:** Free — no per-call charges
- **Coverage:** England & Wales, Scotland, Northern Ireland only. No
  overseas entities.

#### Endpoints and Data Returned

**Search:**

| Endpoint | Purpose |
|---|---|
| `/search/companies` | Company name search, up to 50 hits/call, paginated |
| `/search/officers` | Search by director/officer name |
| `/search/disqualified-officers` | Search disqualified directors |

**Company profile — `/company/{number}`**
- Status, type, incorporation date, registered office address
- SIC codes (industry classification)
- Accounts filing schedule (incl. `next_due`)
- Confirmation-statement schedule
- Previous company names
- Jurisdiction (England & Wales / Scotland / Northern Ireland)
- Insolvency-history flag
- **Detailed status field** — eight possible values: `active`,
  `dissolved`, `liquidation`, `receivership`, `administration`,
  `voluntary-arrangement`, `converted-closed`, `removed`, plus a free-text
  `company_status_detail` (e.g. "active - proposal to strike off") for
  in-flight cases

**Officers — `/company/{number}/officers`**
- Directors, secretaries, and auditors — **current and resigned, by
  default, in the same response**
- Fields: `name`, `officer_role`, `appointed_on`, `resigned_on` (where
  applicable), `nationality`, `country_of_residence`,
  `correspondence_address`
- Date of birth returned as **month + year only** — day is legally
  suppressed under residential-address protection rules (Companies Act
  2006 s.243)
- New `identity_verification_status` field (rolling out from late 2025
  under ECCTA) showing whether a director/PSC has completed identity
  verification

*Filtering officers:*
- No parameters → returns everything, active and resigned, all roles
- `?filter=active` → excludes resigned officers (still returns all roles;
  filter `officer_role=director` client-side if needed)
- Avoid `register_view`/`register_type` for this purpose — both are
  required together, only work if the specific register happens to be
  held at Companies House, and return 404 for many companies otherwise.
  This is a legacy statutory-register feature, not a general
  active/resigned filter.

**Persons with Significant Control (PSC) — `/company/{number}/persons-with-significant-control`**
- Structured beneficial-ownership register: name, kind, nature of control,
  notification dates
- Fully public in the UK (unlike the EU post-CJEU C-37/20, which
  restricted PSC-equivalent data to AML-obliged entities)
- **Important:** PSC is a controlling-influence register, not an equity
  register — it can disagree with actual shareholding (e.g. a 10% direct
  shareholder may not appear in PSC; a corporate trustee with control
  rights may appear in PSC without being a shareholder)

**Filing history — `/company/{number}/filing-history`**
- Every filing: category, type code, date, document ID
- Actual documents fetchable separately as PDF or XHTML/iXBRL bytes

**Charges — `/company/{number}/charges`**
- Registered fixed/floating charges (mortgages, debentures)
- Persons entitled, status (outstanding / satisfied / part-satisfied),
  creation date

#### What's Notably Absent

- **No structured shareholders/equity endpoint** — the statutory members
  register only exists inside the Confirmation Statement (CS01) filing
  document, which must be fetched and parsed manually
- No credit scores, revenue estimates, or financial ratios
- No contact details (phone/email)
- No data on non-UK entities

#### Access Exceptions

Only two things are gated:
1. Bulk annual data dump downloads carry a fee (the same data is available
   free via the per-request API)
2. Two internal-only feeds: the PSC nominee register and the
   names-by-residential-address feed

#### Data Quirks Relevant to Enrichment Logic

**Company number formats are not all 8-digit numerics:**

| Prefix | Entity type |
|---|---|
| 8 digits | Standard England & Wales / Wales-only company |
| `SC` + 6 digits | Scottish company |
| `NI` + 6 digits | Northern Ireland |
| `OC` + 6 digits | LLP |
| `LP` + 6 digits | Limited Partnership |
| `RC` + 6 digits | Registered Society |
| `FC` + 6 digits | Foreign company branch (no UK accounts filed —
statutory dossier is at the home jurisdiction) |
| `NF` + 6 digits | Northern Ireland LLP |

Code that assumes all-numeric company numbers will silently exclude
Scottish companies and every LLP in the country.

#### Relevance to Credit Risk Tiering

- **Director resignation patterns** are visible out of the box via
  `appointed_on`/`resigned_on` on the officers endpoint — useful signal for
  detecting management turnover ahead of other red flags (charges, status
  changes)
- **Detailed status field** (`company_status_detail`) surfaces
  early-warning states like "proposal to strike off" that a simple
  active/dissolved binary would miss
- **Charges data** gives visibility into existing debt obligations
  (mortgages/debentures) against a counterparty
- **PSC vs shareholding mismatch** should be treated as a known
  limitation, not a bug, when building ownership-based risk logic
