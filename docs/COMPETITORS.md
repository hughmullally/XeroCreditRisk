# Xero App Store Competitors — Research Notes

Desk research on Xero-integrated apps that overlap with this app's credit-risk
monitoring space, plus a gap analysis against the current feature set (see
[FEATURES.md](FEATURES.md)).

## Competitive Landscape

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

## What This App Already Does Better

- A genuinely computed 0–100 credit score derived from the org's **own** Xero
  payment history (not third-party) — see [FEATURES.md § Credit Score](FEATURES.md#credit-score)
- Companies House enrichment (status, insolvency history, overdue filings,
  registered charges)
- Real-time updates via Xero webhooks + Server-Sent Events — dashboard rows
  update live as invoices/contacts change
- Syncs risk tiers back into Xero as Contact Groups

None of the apps above combine own-data scoring + Companies House enrichment
+ real-time updates in quite the same way.

## The Gap: No Action Layer

The clearest gap is that this app is purely **observational** — it surfaces
who's risky but takes no action. Every competitor above bundles at least
basic action (automated chasing, payment reminders, collection workflows),
because Xero users shopping in this category are usually trying to *reduce*
bad debt, not just see it coming.

**Secondary, smaller gaps:**
- No third-party-validated credit score (the Creditsafe evaluation — see
  [CREDITSAFE.md](CREDITSAFE.md) — would close this)
- No proactive customer-facing communication at all

**Tradeoff on closing the main gap:** adding a chasing/collections layer
(automated reminders, payment plans) turns this from an internal analytics
tool into direct competition with Chaser and Satago on their own turf — a
much bigger scope and a different kind of product (customer-facing comms,
not just internal reporting). Worth deciding deliberately rather than
drifting into it feature-by-feature.

## Sources

- [Credit Control Software for Xero: How to Choose the Right Tool in 2026 (UK Guide)](https://accounting.events/reviews/credit-control-software-guide/)
- [Debtor Management Apps — Xero App Store](https://apps.xero.com/us/function/debtor-tracking)
- [Top Rated Accounts Receivable Software with Xero 2026 | GetApp](https://www.getapp.com/finance-accounting-software/accounts-receivable/w/xero/)
- [Chaser Pricing (official)](https://www.chaserhq.com/pricing)
- [Satago Pricing (official)](https://www.satago.com/pricing/)
- [Paidnice Pricing (official)](https://www.paidnice.com/pricing)
- [Credit control software compared: Chaser, Kolleno and Upflow (2026) | Trove](https://trove.works/credit-control-software-pricing/)
- [Credit Hound — Sage Intacct Marketplace listing](https://marketplace.intacct.com/MPListing?lid=a2D0H00000DtLy1UAF)
