# Strategy: Expanding Company Data Enrichment Beyond Companies House

*For the Xero-based credit risk extension — moving from UK-only (Companies House) to US, European, and Asian customer coverage*

## The problem

Companies House only covers UK-registered entities. As the credit risk extension expands to US, European, and Asian Xero/QuickBooks/Sage customers, counterparty enrichment, tiering, and payment-behaviour scoring need a data source (or sources) that work outside the UK. Companies House itself has no equivalent reach — <cite>it only provides what companies are legally required to file with the UK registry,</cite> with no credit scores, revenue estimates, or cross-border data.

## The regional gap

| Region | Free/government source | Coverage gap |
|---|---|---|
| **UK** | Companies House (existing) | None — solved |
| **US** | SEC EDGAR | Free, but public companies only. No unified private-company registry — records are fragmented across 50 state Secretary of State offices with inconsistent API access |
| **Europe** | National registries (Handelsregister, INPI/RNE, etc.) | Exist per-country but access quality and API maturity vary widely |
| **Asia** | Jurisdiction-by-jurisdiction | Patchiest coverage; rarely has clean developer-friendly APIs |

**Conclusion:** no free government source will give consistent, credit-relevant coverage across all three regions. A commercial aggregator or credit bureau is needed to fill the gap left by Companies House.

## Provider options evaluated

### Registry aggregators (entity verification, not credit scoring)
- **OpenCorporates** — largest open database, but capped at 500 API calls/month, roughly half its sources no longer updating, no beneficial ownership data. Fine for prototyping only.
- **North Data** — strong for European/German registry data specifically.
- **Zephira** — API-first, developer-friendly, transparent pricing from $99/month (Starter), covering 300M+ companies across 150+ countries, pulling directly from government registries with AI-based cross-jurisdiction normalization. Best pure-registry option if deep credit scoring isn't required.

### Credit/risk-focused providers (closer fit for this use case)
- **Creditsafe** — an actual credit bureau, not just a registry aggregator. Provides credit scores and payment behaviour data directly, which is what the existing counterparty tiering logic needs. Pricing is not published; requires direct quote. Third-party pricing trackers describe a tiered model (Standard/Plus/Premier) with no permanent free tier, though reviewers frequently cite it as notably cheaper than Dun & Bradstreet and Equifax, with a flat-rate model that avoids per-report fee creep.
- **Global Database** — combines registry data (sourced from Companies House, public filings, stock exchanges, proprietary web crawling) with credit scores/limits and contact enrichment in a single API. 400M+ company profiles, 480M+ contacts. Priced via a credit-based subscription model (buy a pool of credits, consumption varies by endpoint). Broader than Creditsafe in scope — could reduce the need for a separate registry + credit bureau integration.
- **Sayari / Moody's Orbis** — go deepest on beneficial ownership and complex risk structures, but oriented more toward compliance/KYB than SME credit scoring. Likely overkill and over-cost for this use case unless customers include higher-risk jurisdictions.

## Recommended approach

**Phase 1 — Get real pricing.**
Neither Creditsafe nor Global Database publish API pricing. The next concrete step is requesting quotes/API documentation directly from both, since the strategy below can't be finalised on desk research alone.

**Phase 2 — Pilot with Global Database first.**
It's the more developer-friendly, API-first option, and — critically — bundles registry verification *and* credit signal in one integration. This avoids maintaining two separate data relationships (a registry aggregator plus a separate credit bureau) during early expansion, which matters given the credit risk extension is still a one-person build.

**Phase 3 — Evaluate Creditsafe as a secondary or premium-tier source.**
If Global Database's credit scoring proves too thin for higher-stakes tiering decisions, add Creditsafe as a secondary source for flagged/high-risk counterparties only — keeping most volume on the cheaper aggregator and reserving bureau-grade reports for cases that need them. This mirrors how Creditsafe's own tiers are structured (limited "Fresh Investigations" at the top tier), suggesting even Creditsafe expects premium reports to be used selectively rather than for full-portfolio coverage.

**Phase 4 — Regional sequencing.**
Given QuickBooks (US) is the next priority integration after Xero, prioritise validating US private-company coverage first — this is the weakest point for both providers and the area most worth stress-testing before committing. European coverage is likely to be stronger out of the gate given both providers' registry-sourced European data; Asian coverage should be treated as a later-stage addition once volume justifies it.

## Cost discipline

Given the metered/credit-based pricing models across every option here (mirroring the QuickBooks reads-metering issue already flagged), the enrichment cost per counterparty lookup should be modelled against expected customer volume *before* committing to a provider — the same discipline applied to the Xero/Sage/QuickBooks API cost analysis. A credit-based model in particular rewards caching and deduplication (e.g. not re-enriching the same counterparty across multiple customer accounts), which should be a design principle from day one rather than a later optimisation.

## Open questions to resolve via provider quotes

1. What does Global Database's credit-based pricing actually cost per lookup at expected volume (dozens to low hundreds of counterparties per customer)?
2. Does Creditsafe's API pricing scale down to a solo-developer/early-stage volume, or is it enterprise-only in practice?
3. How current is each provider's US private-company data, given the absence of a unified US registry?
4. Do either provider's terms restrict use of their data for training AI/ML models or building a resellable product (as Xero's new terms now do) — this needs checking before building deeper dependency on either.
