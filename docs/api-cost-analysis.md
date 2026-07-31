# API Cost Analysis: Xero vs Sage vs QuickBooks

*For the Xero-based credit risk extension — comparison of integration costs across accounting platforms*

## Summary

| | Xero | Sage Business Cloud | QuickBooks Online |
|---|---|---|---|
| **Base cost** | Free (Starter tier) | Free | Writes free; reads metered |
| **Cost at small scale** | Free up to 5 connections | Free | Free up to 500K reads/month |
| **Cost at growth scale** | $35 AUD/mo (~£18) up to 50 connections | Free (capped by rate limits, not fees) | Paid tiers required beyond 500K reads/month, up to $4,500/mo |
| **Cost at large scale** | $245 AUD/mo up to 1,000 connections; $1,445 AUD/mo up to 10,000 | Free | Higher paid tier (Silver/Gold/Platinum), exact pricing not published |
| **Overage pricing** | $2.40 AUD/GB of data egress beyond tier allowance | N/A (hard rate-limited instead) | N/A (tiered reads model) |

## Xero — detailed tier breakdown

Pricing took effect 2 March 2026, replacing the previous free/revenue-share model.

| Tier | Monthly fee | Max connections | Rate limit | Egress allowance |
|---|---|---|---|---|
| Starter | Free | 5 | 1,000 calls/day/org | n/a |
| Core | $35 AUD | 50 | 5,000 calls/day/org | 10 GB |
| Plus | $245 AUD | 1,000 | 5,000 calls/day/org | 50 GB |
| Advanced | $1,445 AUD | 10,000 | 5,000 calls/day/org | 250 GB |
| Enterprise | Custom | Unlimited | 5,000 calls/day/org | Custom |

**Key gotchas:**
- The Journal endpoint (raw journal entries) is locked behind the Advanced tier and requires a security assessment — only relevant if the credit scoring logic needs journal-level data rather than just invoices/payments.
- Existing apps were migrated to this model with 30 days' notice; new apps go straight onto it.

## Sage Business Cloud Accounting

- **API access is free**, but constrained by tight limits: ~2,500 requests/day per company, 100/minute.
- No webhooks — polling only, using `updated_from`/`updated_to` filters.
- Main cost is engineering time (5-minute access tokens, manual re-authorization if refresh tokens lapse after 31 days), not licensing fees.
- Distribution requires Sage partner certification and Global Marketplace listing.

## QuickBooks Online

- **Writing data remains free.** **Reading data is metered** under a tiered model introduced in 2025:
  - Free "Builder" tier: 500,000 reads/month
  - Paid tiers (Silver, Gold, Platinum): required beyond that, with published pricing not fully disclosed in available sources
  - Overall App Partner Program cost range cited elsewhere: $0–$4,500/month depending on scale
- Rate limit is generous relative to Sage: 500 requests/min per company, 10 concurrent.
- For a credit-risk product continuously polling invoices/payments across many customer companies, this metered-read model is the one most likely to generate a real, ongoing cost line as usage grows — worth modelling against expected customer count before committing engineering time.

## Bottom line for the credit risk extension

- **Xero** is cheap through early growth — free to 5 customers, ~£18/month to 50 — and scales predictably and transparently after that.
- **Sage** has no direct licensing cost, but its low rate limits and lack of webhooks make it more of an engineering cost than a fee cost.
- **QuickBooks** is the most commercially important target for US penetration, but is also the platform where read costs need the most careful forecasting before scaling up.
