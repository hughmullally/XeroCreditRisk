# Architecture & Codebase Guide

Technical reference for how `XeroExtension.Web` is put together. See
`FEATURES.md` for what it does and `CLAUDE.md` for stack/setup basics.

## Stack

- .NET 10 / ASP.NET Core Web API, controller-based (no MVC views — every
  HTML response is a hand-built string returned as `ContentResult`)
- `Xero.NetStandard.OAuth2` SDK (v14+) for OAuth 2.0 and the Accounting API
- `Microsoft.Extensions.Caching.Memory` for the token store and nothing else
  persistent — there is no database in this project

## Project layout

```
src/XeroExtension.Web/
  Controllers/    HTTP endpoints — thin, delegate to Services
  Services/       All business logic and external API calls
  Models/         Plain data classes passed between Services and Controllers
  Program.cs      DI container wiring, middleware pipeline
```

Standard three-layer separation: Controllers never call Xero or Companies
House directly, and Services never touch `HttpContext` or build HTML.

## Dependency injection (`Program.cs`)

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

## Authentication & token lifecycle

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

## Xero SDK integration notes

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

## The credit risk domain layer (`CreditRiskService`)

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

### Method dependency graph

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

### `GetContactRiskAsync`

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

### `GetPaymentTrendAsync`

Looks only at `PAID` invoices with both a due date and a paid date. Splits
each contact's chronological payment history in half and compares the
average lateness of the second half against the first — a positive
`TrendDelta` means payments are getting later, negative means earlier.
Contacts with fewer than 2 paid invoices get `TrendDelta = 0` (not enough
signal).

### `GetCreditScoresAsync`

Starts every contact at a baseline of 100 and applies a fixed set of
deductions/bonuses (risk tier, payment trend, concentration, three
Companies House signals), clamps to 0–100, and buckets into an A–F grade.
Each deduction is recorded as a human-readable string in
`ContactCreditScore.Reasons`, in application order, which is what the
dashboard's score drilldown renders verbatim — the reasons list isn't a
separate explanation generated after the fact, it's produced by the same
`Apply(delta, label)` local function that mutates the running score, so it
can never drift out of sync with the number it explains.

### `GetCreditLimitRecommendationsAsync`

Groups sales invoices (`AUTHORISED` or `PAID`) by contact, takes the
average invoice value, and scales it by `(score / 100) × 3.0` — a
continuous multiplier rather than the four discrete risk-tier multipliers
used before scoring existed. Rounds up to the nearest £100. Builds the
same kind of `Reasons` breakdown as the score, for its own drilldown.
Contacts with no outstanding invoices (so absent from the risk/score
lists) default to a score of 100, matching the "Current, no issues"
treatment used elsewhere.

### `GetEarlyWarningsAsync`

Not a scoring model — a flat list of independently-triggered
`EarlyWarningTrigger` records, aggregated from risk, trend, and
recommendation data: first-ever late payment, accelerating lateness,
exceeding the recommended limit, Companies House distress/insolvency
signals, and concentration risk. Each warning carries a free-text
`Message` composed at trigger time.

### `SyncRiskGroupsToXeroAsync`

The only method in this service that writes anything back to Xero. Maps
each `CreditRiskLevel` to a fixed group name (`Risk: High` etc.) and calls
`XeroService.EnsureContactGroupAsync` / `ReplaceContactGroupMembersAsync`
once per tier.

## Companies House integration

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

## Real-time update pipeline

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

## Dashboard rendering (`DashboardController`)

There's no view engine. `Index` builds the entire HTML document as one
big C# string and returns it via `Content(html, "text/html")`. A few
conventions worth knowing before touching this file:

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

## Dev/demo tooling (`DevController`)

Explicitly out-of-scope for the real feature set (see its doc comment).
Provides a UI and endpoints for seeding test invoices across a fixed due-
date offset pattern (so seeded data spans every risk tier by construction)
and for assigning Companies House company numbers to demo contacts from a
pool of verified real companies. Two contacts (`City Limousines`,
`Ridgeway University`) are hardcoded as permanent problem-scenario demo
cases; everything else in the bulk-populate pool is genuinely clean.

## Where to extend

- **Multi-tenancy**: replace the `UserId` constant in `AuthController` and
  `XeroService` with a value from `HttpContext.User` claims.
- **Persistent tokens**: implement `ITokenStore` against a real store and
  swap the `AddSingleton<ITokenStore, InMemoryTokenStore>()` registration.
- **New Xero data**: add methods to `IXeroService`/`XeroService` — any
  class under `Xero.NetStandard.OAuth2.Api` is available via the SDK.
- **New risk signals**: add a factor inside `GetCreditScoresAsync`'s
  `Apply(...)` calls — it automatically appears in every score's
  `Reasons` drilldown with no other changes needed.
