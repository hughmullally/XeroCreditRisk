# PoC Status and Improvement Strategy

*Summary of current functionality and a forward strategy, synthesised from
this project's working session — including two pre-existing strategy docs
([beyond-companies-house-strategy.md](beyond-companies-house-strategy.md),
[api-cost-analysis.md](api-cost-analysis.md)) that already had a plan in
motion before the Creditsafe/competitor research below.*

## Current PoC Functionality

### Core product (the credit risk dashboard itself)

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

See [FEATURES.md](FEATURES.md) for full detail.

### Infrastructure

- Deployed on Azure App Service (Basic B1, Always On) —
  [xero-extension-dev.azurewebsites.net](https://xero-extension-dev.azurewebsites.net)
- Secrets in Key Vault; Xero tokens persisted in Blob Storage (survives
  restarts — previously reset every deploy)
- Entra ID login gate — currently restricted to just `hugh@mohober.com`;
  **a second user's access is still pending**, deferred by choice
- Webhook endpoint correctly excluded from the login gate (self-authenticates
  via HMAC signature, since it's a server-to-server call from Xero)

## Strategy for Improving It

### 1. Close the "action layer" gap (biggest finding from competitor research)

Every real competitor (Chaser, Satago, Paidnice) bundles some action —
automated chasing, reminders, payment plans — not just risk visibility. This
PoC is purely observational. This is the single highest-leverage gap, but
also the biggest scope decision: it turns this from an internal tool into
direct competition with Chaser/Satago. See [COMPETITORS.md](COMPETITORS.md)
for the full comparison and pricing.

### 2. Add a third-party credit score — reconcile with the existing sequencing

[beyond-companies-house-strategy.md](beyond-companies-house-strategy.md)
recommends **Global Database first** (registry + credit score bundled,
cheaper, more developer-friendly), with **Creditsafe as a secondary/premium
source** for flagged high-risk counterparties only — not the other way
round. Worth reconciling that existing plan with this session's
Creditsafe-specific research ([CREDITSAFE.md](CREDITSAFE.md),
[CREDITSAFE_JUSTIFICATION.md](CREDITSAFE_JUSTIFICATION.md)) before
committing to either.

### 3. International expansion is already scoped, gated on real pricing

UK is solved (Companies House). US/Europe/Asia need a commercial provider
since no free registry covers them consistently. Next concrete step per
that doc: get real quotes from Global Database and Creditsafe — desk
research can't finish this alone.

### 4. Multi-platform expansion (QuickBooks next, then Sage)

[api-cost-analysis.md](api-cost-analysis.md) flags QuickBooks' metered-reads
pricing (free to 500K reads/month, up to $4,500/mo beyond) and Sage's tight
rate limits (2,500 req/day, no webhooks — polling only) as real engineering
constraints, not just licensing costs. QuickBooks is prioritised next
specifically because US private-company data coverage is the weakest point
worth stress-testing early.

### 5. Near-term, smaller items

- Decide on additional user access (or anyone else) — still pending
- Multi-tenancy: today's `UserId`/token-store model assumes one shared Xero
  org; real multi-user/multi-org use would need the token store keyed per
  authenticated user, not a fixed constant
- Cost discipline principle from the strategy doc — cache/dedupe
  counterparty enrichment lookups from day one, since every provider
  considered uses metered/credit-based pricing
