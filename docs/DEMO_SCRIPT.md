# Demo Script — Xero Credit Risk Dashboard

A ~8–10 minute walkthrough script. "Say" lines are talking points, not a
verbatim transcript — adapt to the room. "Do" lines are the click-by-click
actions, in the order the dashboard actually lays them out top to bottom.

## Before you start

- [ ] Xero org is connected and has demo data seeded (`/dev?tenantId={id}` →
  seed invoices, then populate company numbers) so every risk tier, the
  charts, Chase First, and Early Warnings all have something to show.
- [ ] Browser at a reasonable width — the charts row and table both look
  cramped below ~1000px.
- [ ] Know your tenant ID / have the landing page open at `/` so you can
  click through rather than pasting URLs live.
- [ ] If you plan to show live updates, a public tunnel (e.g. `devtunnel`)
  is running and registered as the Xero webhook URL. This is the one part
  of the demo worth skipping if you haven't tested it beforehand.

---

## 1. The problem (30 seconds)

**Say:** Late payment isn't a bookkeeping footnote — UK SMEs currently have
roughly half their outstanding invoices sitting overdue at any given time,
and businesses affected by it forgo an average of around 3% of annual
turnover in investment they can't make because the cash is stuck in someone
else's business. Xero already has all the data to see this coming — it's
just not surfaced anywhere.

**Do:** Nothing yet — this is spoken framing before you touch the screen.

---

## 2. Landing page (20 seconds)

**Do:** Open `/`.

**Say:** This is the front door — Connect to Xero for OAuth, then the
Dashboard itself, a seed/dev tool we use for demo data, and the project
docs.

**Do:** Click into **📊 Dashboard**.

---

## 3. Portfolio charts (1 minute)

**Do:** Point at the two charts row at the top of the dashboard — Aged
Debtors and Risk Segments.

**Say:** Before you look at a single customer, this answers "how healthy is
my whole book right now?" Aged Debtors breaks every outstanding invoice into
age bands — not yet due, through to 90+ days. Risk Segments does the same
by risk tier, using the same colors as the badges you'll see in the table,
so the whole page reads consistently. Both are individual bars scaled to
the largest value in the group, not a stacked bar — easier to compare at a
glance than trying to eyeball segment widths.

---

## 4. Chase First (1 minute)

**Do:** Scroll to the 🎯 Chase First panel.

**Say:** This answers a different question: "if I only have time to chase
five people today, who are they?" It's ranked by overdue amount times days
overdue — the money at stake, weighted by urgency — deliberately a simple,
explainable ranking rather than a blended score mixing pounds and an
abstract 0–100 number.

**Do:** Click one of the rows.

**Say:** That jumps straight to the customer's row in the table below and
opens their reminder draft — which we'll come back to.

---

## 5. Early Warnings (1.5 minutes)

**Do:** Scroll to the ⚠ Early Warnings panel.

**Say:** This is for the things that haven't shown up in a risk tier yet
but should worry you anyway — a reliable payer's first-ever late payment,
lateness accelerating faster than their tier reflects, someone already over
their recommended limit, or a Companies House distress signal. It's
collapsible and can be grouped by counterparty or by warning type.

**If it's currently showing the overdue-invoices line at the top:**

**Say:** And when the portfolio as a whole is running worse than the UK SME
average — currently around 49% of invoices overdue, per Sage/CEBR's
analysis of 1.2 million+ invoices — that shows up here too, as a warning in
its own right rather than a permanent fixture on the page regardless of
whether there's anything to worry about.

---

## 6. The main table (3–4 minutes — the core of the demo)

**Do:** Scroll to the table. Walk the columns left to right, clicking to
expand where noted.

- **Contact** — links straight out to the record in Xero.
- **Score** — click to expand. **Say:** every score is a 0–100 number with
  an A–F grade, built from a transparent set of deductions off a baseline
  of 100 — risk tier, payment trend, concentration, and any Companies
  House distress signals. The breakdown shows exactly which factors applied
  to this specific customer and by how much — nothing is a black box.
- **Outstanding** / **Concentration** — **Say:** concentration is often the
  one people miss — a customer paying perfectly on time can still be a
  risk if they're 40% of your total exposure.
- **Overdue** / **Oldest Overdue (days)**.
- **Risk** — click the badge to expand. **Say:** same idea as the score —
  Current/Low/Medium/High isn't just a color, it's derived from oldest
  overdue days, escalated automatically if Companies House shows the
  company in distress, and the reasoning is one click away.
- **Payment Trend** — **Say:** average days late plus a trend arrow and
  sparkline, comparing the second half of their payment history against the
  first half, so you can see a customer drifting worse before it's obvious
  in the raw numbers.
- **Recommended Limit** — click to expand. **Say:** Xero has no native
  credit limit field, so this is computed — average invoice size scaled by
  the credit score, capped at 3× for a perfect score, tightening toward
  zero as the score drops. If someone's already over it, that's flagged
  here and raised as an Early Warning.
- **Companies House** — **Say:** for any contact with a company number on
  file, this is live UK company registry data — trading status,
  incorporation age, overdue statutory filings, registered charges, and
  prior insolvency history, even for a company trading normally today.
- **Reminder** — click **✉ Draft** on an overdue row. **Say:** a ready-to-send
  chase email, pre-filled with the invoice specifics — draft only, nothing
  sends automatically, so there's a human in the loop before anything goes
  out.

**Do:** Click a column header to demonstrate sorting.

**Say:** Every column sorts — useful once you're working from Chase First
or Early Warnings and want to reorder around whatever's caught your eye.

---

## 7. How this compares to Chaser / Satago (1 minute, if asked "why not just use X")

**Say:** The closest things on the Xero App Store are Chaser and Satago —
Chaser starts at £199/month and works off the same Companies House data
we're already pulling in here; Satago's scoring runs on Experian credit
reports instead. Where we're ahead of both: the credit score here is
computed from the org's **own** Xero payment history, not a third-party
report, and it updates live off Xero webhooks rather than a scheduled
sync — plus the risk tiers sync straight back into Xero as Contact Groups,
which neither of them does.

**Say:** Where they're ahead of us, honestly: both are built around an
active chasing workflow — automated reminder sequences, escalating
touchpoints, in Satago's case invoice finance against unpaid invoices. What
you've seen today is observational — it tells you who to chase and hands
you a drafted email, a human still sends it. Turning that into automated,
scheduled chasing would put us in direct competition with them on their
own turf, so that's a deliberate scope call, not a gap we've missed.

**Say (if pressed on why we haven't just built the chasing layer):** A few
real downsides, not just "we haven't gotten to it yet":

- **It's a different product, not a feature.** The moment reminders send
  automatically, this stops being an internal analytics tool and becomes
  customer-facing communication going out under someone else's brand —
  deliverability, bounce handling, unsubscribe/opt-out, all the
  infrastructure that comes with actually emailing someone's customers, not
  just showing you a number.
- **We'd be going head-on against funded incumbents on their own turf.**
  Chaser starts at £199/month and scales to £899; Satago's playbook adds
  invoice finance on top. Competing there directly is a much bigger fight
  than staying the better risk-intelligence layer that can sit alongside
  either of them.
- **Higher blast radius when it goes wrong.** A bug in a dashboard number is
  embarrassing; a bug in an automated chasing sequence damages an actual
  customer relationship, and it's a lot harder to walk back an email that's
  already sent than a stat that's briefly wrong on screen.
- **It's a different compliance surface.** Tone, frequency, and content of
  automated debt-chasing communications is territory worth getting properly
  advised on before shipping — not something to back into feature-by-feature.
- **It dilutes what's actually differentiated today.** The honest pitch
  right now is "better risk detection so you know who to chase and how
  urgently" — genuinely useful standalone or paired with a tool like Chaser.
  Chasing well takes real, ongoing product investment; better to keep
  drafting sharp reminders and let a specialist tool own the sending, at
  least for now.

---

## 8. Optional: writing risk tiers back to Xero (30 seconds, if asked)

**Say:** There's also a sync that writes each contact's current risk tier
back into Xero itself as Contact Groups — Risk: High / Medium / Low /
Current — so the categorisation is visible natively in Xero's own Contacts
screen for anyone who never opens this dashboard.

## 9. Optional: live updates (30 seconds, only if the tunnel is confirmed working)

**Say:** The dashboard isn't a static report — it's wired to Xero's
webhooks, so a new invoice or a payment updates the page in real time via a
live event stream, with the affected row briefly highlighted so you know
what just changed without a manual refresh.

**Do:** Only demo this live if you've verified the tunnel and webhook
registration beforehand — it's the one moving part outside your control on
demo day.

---

## Close

**Say:** Everything you've seen is built entirely on data that's already in
Xero, enriched with public UK company data — no manual data entry, no
separate system to maintain. The next step is [tailor to audience: pricing
model / pilot org / roadmap item].
