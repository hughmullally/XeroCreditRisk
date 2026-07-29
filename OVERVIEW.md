# Xero Credit Risk Dashboard — Overview

A tool that helps you spot which customers are becoming a credit risk before
it turns into a bad debt — using the invoice data already in Xero, enriched
with UK company registry information, kept up to date automatically.

## Connecting your Xero account

A one-time sign-in through Xero's own secure login screen. Once connected,
the dashboard reads your sales invoices and customer records directly — no
data entry required.

## The Credit Risk Dashboard

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

## Early Warnings

A highlighted panel above the table flags customers showing early signs of
trouble — before it's serious enough to show up in their overall risk
rating. This includes a reliably-paying customer's first ever late payment,
a customer whose payments are getting slower at an accelerating rate, a
customer who has gone over their recommended credit limit, or worrying
information from the companies register. Warnings can be grouped either by
customer or by type of warning, and the panel can be collapsed once
reviewed.

## How the Credit Score works

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

## How the Recommended Credit Limit works

The suggested limit is based on how much this customer typically orders,
scaled up or down according to their Credit Score — a customer with a
strong score can responsibly be offered a higher multiple of their typical
order value, while a customer with a poor score is offered little to no
further credit. If a customer already owes more than their recommended
limit, that's flagged directly on the dashboard and raised as an Early
Warning.

## Company background checks

Where a customer's UK company registration number is on file, their record
is automatically checked against the public companies register for their
current trading status, how long they've been established, whether they're
behind on their own accounts or confirmation statement filings, whether
they have any registered charges (security given to a lender) against them,
and whether they've ever previously been through insolvency — even if
they're trading normally today.

## Always up to date

The dashboard refreshes itself automatically the moment something relevant
changes in Xero — a new invoice, a payment, a change to a customer record —
with no need to manually reload the page.
