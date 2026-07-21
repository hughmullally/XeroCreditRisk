# Xero Extension — Claude Code Guide

## Project Overview
ASP.NET Core Web API that integrates with Xero via OAuth 2.0 (Authorization Code flow).
Uses the official `Xero.NetStandard.OAuth2` SDK (v14+).

## Stack
- .NET 10 / ASP.NET Core Web API
- `Xero.NetStandard.OAuth2` NuGet package
- In-memory token store (swap for persistent store in production)

## Project Structure
```
XeroExtension.sln
src/
└── XeroExtension.Web/
    ├── Controllers/
    │   ├── AuthController.cs      # OAuth connect/callback/disconnect endpoints
    │   └── XeroController.cs      # Invoices, contacts, accounts API endpoints
    ├── Models/
    │   └── XeroTokenSet.cs        # Token storage model
    ├── Services/
    │   ├── ITokenStore.cs         # Token persistence interface
    │   ├── InMemoryTokenStore.cs  # In-memory implementation (dev only)
    │   ├── IXeroService.cs        # Xero API service interface
    │   └── XeroService.cs         # Xero API calls + token refresh logic
    ├── Program.cs                 # DI wiring and middleware pipeline
    └── appsettings.json           # Xero ClientId/Secret/Scopes config
```

## OAuth 2.0 Flow
1. `GET /auth/xero/connect` → redirects user to Xero login
2. Xero redirects back to `GET /auth/xero/callback?code=...`
3. Tokens are exchanged and stored via `ITokenStore`
4. All API calls in `XeroService` auto-refresh expired tokens

## Configuration
Fill in `appsettings.json` (or use user-secrets / environment variables):
```json
"Xero": {
  "ClientId": "...",
  "ClientSecret": "...",
  "CallbackUri": "https://localhost:7213/auth/xero/callback",
  "Scope": "openid profile email accounting.invoices.read accounting.contacts.read accounting.settings.read offline_access"
}
```
Register the redirect URI in your Xero Developer app: https://developer.xero.com/app/manage

**Scope note:** Xero apps created after 2 March 2026 only have access to the new granular
per-resource scopes (`accounting.invoices.read`, `accounting.contacts.read`,
`accounting.settings.read`, etc.) — the old broad scopes like `accounting.transactions` no
longer work for these apps and the authorize endpoint returns `invalid_scope`. Apps created
before that date keep the broad scopes until September 2027. Check your app's Configuration
page on the Xero Developer portal for the exact scope names it supports if you add new API
calls.

## Running Locally
```bash
cd src/XeroExtension.Web
dotnet run
```

## Key Extension Points
- **Token persistence**: Replace `InMemoryTokenStore` with a DB-backed implementation (`ITokenStore`)
- **Multi-tenancy**: The `UserId` constant in `AuthController` and `XeroService` should be replaced with `HttpContext.User` claims
- **More Xero APIs**: Add methods to `IXeroService` / `XeroService` — all Xero API classes are available via `Xero.NetStandard.OAuth2.Api`
- **Auth for API endpoints**: Add `[Authorize]` attributes and configure cookie/JWT auth for your users

## Common Xero API Namespaces
- `Xero.NetStandard.OAuth2.Api.AccountingApi` — invoices, contacts, accounts, payments
- `Xero.NetStandard.OAuth2.Api.PayrollAuApi` / `PayrollNzApi` / `PayrollUkApi` — payroll
- `Xero.NetStandard.OAuth2.Api.AssetApi` — fixed assets
- `Xero.NetStandard.OAuth2.Api.ProjectApi` — projects
