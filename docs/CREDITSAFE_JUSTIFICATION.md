# Why Creditsafe Over Dun & Bradstreet or Experian Business

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

## Caveat

This is a lean, desk-research comparison — not a vendor evaluation. Before
committing, get real pricing and a sandbox trial from Creditsafe, and at
least a pricing conversation with D&B/Experian, to confirm points 1 and 3
hold up under direct contact.
