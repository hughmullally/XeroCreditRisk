using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using XeroExtension.Web.Models;
using XeroExtension.Web.Services;

namespace XeroExtension.Web.Controllers;

[Route("dashboard")]
public class DashboardController : ControllerBase
{
    private readonly ICreditRiskService _creditRiskService;
    private readonly DashboardNotifier _notifier;

    public DashboardController(ICreditRiskService creditRiskService, DashboardNotifier notifier)
    {
        _creditRiskService = creditRiskService;
        _notifier = notifier;
    }

    /// <summary>
    /// GET /dashboard/events — Server-Sent Events stream. Sends "data: changed" whenever the Xero
    /// webhook receiver processes an event, so open dashboard tabs can refresh themselves.
    /// </summary>
    [HttpGet("events")]
    public async Task Events(CancellationToken cancellationToken)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";

        var changed = new TaskCompletionSource<IReadOnlyList<string>>();
        void OnChanged(IReadOnlyList<string> contactIds) => changed.TrySetResult(contactIds);
        _notifier.Changed += OnChanged;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var finished = await Task.WhenAny(changed.Task, Task.Delay(TimeSpan.FromSeconds(25), cancellationToken));

                if (finished == changed.Task)
                {
                    var payload = JsonSerializer.Serialize(new { contactIds = changed.Task.Result });
                    await Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
                    changed = new TaskCompletionSource<IReadOnlyList<string>>();
                }
                else
                {
                    await Response.WriteAsync(": keep-alive\n\n", cancellationToken);
                }

                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — expected when the browser tab closes or reloads.
        }
        finally
        {
            _notifier.Changed -= OnChanged;
        }
    }

    /// <summary>GET /dashboard?tenantId={id} — credit risk table with deep links into Xero contact records.</summary>
    [HttpGet]
    public async Task<ContentResult> Index([FromQuery] string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return Content("<p>Missing required query parameter: tenantId</p>", "text/html; charset=utf-8");

        var risk = await _creditRiskService.GetContactRiskAsync(tenantId);
        var trends = await _creditRiskService.GetPaymentTrendAsync(tenantId);
        var trendByContact = trends.ToDictionary(t => t.ContactId);
        var recommendations = await _creditRiskService.GetCreditLimitRecommendationsAsync(tenantId);
        var recommendationByContact = recommendations.ToDictionary(r => r.ContactId);
        var warnings = await _creditRiskService.GetEarlyWarningsAsync(tenantId);
        var scores = await _creditRiskService.GetCreditScoresAsync(tenantId);
        var scoreByContact = scores.ToDictionary(s => s.ContactId);

        var totalOutstandingInvoices = risk.Sum(r => r.OutstandingInvoiceCount);
        var totalOverdueInvoices = risk.Sum(r => r.OverdueInvoiceCount);
        var benchmarkSection = totalOutstandingInvoices == 0 ? "" : BuildBenchmarkSection(totalOverdueInvoices, totalOutstandingInvoices);

        var warningsByContact = warnings
            .GroupBy(w => new { w.ContactId, w.ContactName })
            .OrderByDescending(g => g.Count())
            .ToList();

        var warningGroupsByContact = string.Join("\n", warningsByContact.Select(g => $"""
            <details>
              <summary>
                <a href="https://go.xero.com/Contacts/Edit.aspx?contactID={g.Key.ContactId}" target="_blank" onclick="event.stopPropagation()">{WebUtility.HtmlEncode(g.Key.ContactName)}</a>
                <span class="warning-count">{g.Count()}</span>
              </summary>
              <ul>
                {string.Join("\n", g.Select(w => $"<li>{WebUtility.HtmlEncode(w.Message)}</li>"))}
              </ul>
            </details>
            """));

        var warningsByType = warnings
            .GroupBy(w => w.Type)
            .OrderByDescending(g => g.Count())
            .ToList();

        var warningGroupsByType = string.Join("\n", warningsByType.Select(g => $"""
            <details>
              <summary>
                {WarningTypeLabel(g.Key)}
                <span class="warning-count">{g.Count()}</span>
              </summary>
              <ul>
                {string.Join("\n", g.Select(w => $"""<li><a href="https://go.xero.com/Contacts/Edit.aspx?contactID={w.ContactId}" target="_blank">{WebUtility.HtmlEncode(w.ContactName)}</a>: {WebUtility.HtmlEncode(w.Message)}</li>"""))}
              </ul>
            </details>
            """));

        var warningsSection = warnings.Count == 0 ? "" : $"""
            <div class="warnings">
              <h2>
                ⚠ Early Warnings ({warnings.Count} across {warningsByContact.Count} counterparties)
                <button id="warningsToggle" class="warnings-toggle-btn">▼ Collapse</button>
              </h2>
              <div id="warningsContent">
                <div class="group-toggle">
                  <label><input type="radio" name="groupBy" value="contact" checked /> By Counterparty</label>
                  <label><input type="radio" name="groupBy" value="type" /> By Warning Type</label>
                </div>
                <div class="warnings-list" id="warningsByContact">
                  {warningGroupsByContact}
                </div>
                <div class="warnings-list" id="warningsByType" style="display:none">
                  {warningGroupsByType}
                </div>
              </div>
            </div>
            """;

        var rows = string.Join("\n", risk.Select(r =>
        {
            var trendCell = trendByContact.TryGetValue(r.ContactId, out var trend)
                ? $"""<span class="trend {TrendClass(trend.TrendDelta)}">{Math.Round(trend.AverageDaysLate, 1)} days avg {TrendLabel(trend.TrendDelta)}</span>"""
                : """<span class="muted">No payment history</span>""";

            var limitCell = recommendationByContact.TryGetValue(r.ContactId, out var rec)
                ? $"""
                    <details class="limit-drilldown">
                      <summary>
                        <span class="{(rec.ExceedsRecommendedLimit ? "limit-exceeded" : "")}">
                          £{rec.RecommendedCreditLimit:N2}{(rec.ExceedsRecommendedLimit ? " ⚠" : "")}
                        </span>
                      </summary>
                      <ul>
                        {string.Join("\n", rec.Reasons.Select(reason => $"<li>{WebUtility.HtmlEncode(reason)}</li>"))}
                      </ul>
                    </details>
                    """
                : """<span class="muted">—</span>""";

            var chCell = r.CompanyNumber is null
                ? """<span class="muted">No company number</span>"""
                : r.CompaniesHouseStatus is null
                    ? $"""<span class="muted">{WebUtility.HtmlEncode(r.CompanyNumber)} (not found)</span>"""
                    : $"""
                        <span class="ch-status {(r.CompaniesHouseDistressed ? "distressed" : r.CompaniesHouseOverdueFilings ? "overdue-filings" : "healthy")}">
                          {WebUtility.HtmlEncode(r.CompaniesHouseStatus)}{(r.CompaniesHouseOverdueFilings ? " ⚠ filings overdue" : "")}
                        </span>
                        {(r.CompanyIncorporationDate is { } inc ? $"""<div class="ch-meta">Est. {inc:yyyy} ({(int)((DateTime.UtcNow - inc).TotalDays / 365.25)}y)</div>""" : "")}
                        {(r.CompaniesHouseHasInsolvencyHistory ? """<div class="ch-meta warn">⚠ Prior insolvency</div>""" : "")}
                        {(r.CompaniesHouseHasCharges ? """<div class="ch-meta">🔒 Has registered charges</div>""" : "")}
                        """;

            var concentrationCell = $"""<span class="concentration {ConcentrationClass(r.ConcentrationPercent)}">{r.ConcentrationPercent:0.#}%</span>""";

            var riskCell = $"""
                <details class="risk-drilldown">
                  <summary><span class="badge {r.RiskLevel.ToString().ToLowerInvariant()}">{r.RiskLevel}</span></summary>
                  <ul>
                    {string.Join("\n", r.Reasons.Select(reason => $"<li>{WebUtility.HtmlEncode(reason)}</li>"))}
                  </ul>
                </details>
                """;

            var trendSortValue = trendByContact.TryGetValue(r.ContactId, out var trendForSort) ? trendForSort.AverageDaysLate.ToString() : "";
            var limitSortValue = recommendationByContact.TryGetValue(r.ContactId, out var recForSort) ? recForSort.RecommendedCreditLimit.ToString() : "";
            var chSortValue = CompaniesHouseSortRank(r);

            var scoreCell = scoreByContact.TryGetValue(r.ContactId, out var score)
                ? $"""
                    <details class="score-drilldown">
                      <summary><span class="score-badge {GradeClass(score.Grade)}">{score.Score} ({score.Grade})</span></summary>
                      <ul>
                        {string.Join("\n", score.Reasons.Select(reason => $"<li>{WebUtility.HtmlEncode(reason)}</li>"))}
                      </ul>
                    </details>
                    """
                : """<span class="muted">—</span>""";
            var scoreSortValue = scoreByContact.TryGetValue(r.ContactId, out var scoreForSort) ? scoreForSort.Score.ToString() : "";

            return $"""
                <tr data-contact-id="{r.ContactId}">
                  <td data-sort-value="{WebUtility.HtmlEncode(r.ContactName)}"><a href="https://go.xero.com/Contacts/Edit.aspx?contactID={r.ContactId}" target="_blank">{WebUtility.HtmlEncode(r.ContactName)}</a></td>
                  <td data-sort-value="{scoreSortValue}">{scoreCell}</td>
                  <td data-sort-value="{r.OutstandingAmount}">£{r.OutstandingAmount:N2}</td>
                  <td data-sort-value="{r.ConcentrationPercent}">{concentrationCell}</td>
                  <td data-sort-value="{r.OverdueAmount}">£{r.OverdueAmount:N2}</td>
                  <td data-sort-value="{r.OldestOverdueDays}">{r.OldestOverdueDays}</td>
                  <td data-sort-value="{(int)r.RiskLevel}">{riskCell}</td>
                  <td data-sort-value="{trendSortValue}">{trendCell}</td>
                  <td data-sort-value="{limitSortValue}">{limitCell}</td>
                  <td data-sort-value="{chSortValue}">{chCell}</td>
                </tr>
                """;
        }));

        var html = $$"""
            <!DOCTYPE html>
            <html>
            <head>
              <meta charset="utf-8" />
              <title>Credit Risk Dashboard</title>
              <style>
                :root {
                  color-scheme: light dark;
                  --page: #eef1f5;
                  --surface: #ffffff;
                  --text-primary: #0b0b0b;
                  --text-secondary: #52514e;
                  --text-muted: #898781;
                  --border: #e1e0d9;
                  --accent: #13b5ea;
                  --header-start: #0a4d75;
                  --header-end: #13b5ea;
                  --good: #0ca30c;
                  --warning: #fab219;
                  --serious: #ec835a;
                  --critical: #d03b3b;
                  --shadow: 0 4px 14px rgba(11,11,11,0.09), 0 1px 3px rgba(11,11,11,0.06);
                  --highlight: #fff3b0;
                }
                @media (prefers-color-scheme: dark) {
                  :root {
                    --page: #0d0d0d;
                    --surface: #1a1a19;
                    --text-primary: #ffffff;
                    --text-secondary: #c3c2b7;
                    --text-muted: #898781;
                    --border: #2c2c2a;
                    --accent: #4bc8f5;
                    --header-start: #062f47;
                    --header-end: #0f7fa3;
                    --shadow: 0 4px 14px rgba(0,0,0,0.5), 0 1px 3px rgba(0,0,0,0.35);
                    --highlight: #4a3f0a;
                  }
                }
                * { box-sizing: border-box; }
                body {
                  font-family: system-ui, -apple-system, "Segoe UI", sans-serif;
                  margin: 0;
                  padding: 0;
                  background: var(--page);
                  color: var(--text-primary);
                  line-height: 1.5;
                  font-size: 15.5px;
                }
                .app-header {
                  background: linear-gradient(135deg, var(--header-start), var(--header-end));
                  padding: 1.6rem clamp(1rem, 3vw, 2.5rem);
                  box-shadow: 0 2px 10px rgba(0,0,0,0.18);
                }
                .app-header-inner {
                  display: flex; align-items: center; gap: 0.9rem; flex-wrap: wrap;
                }
                h1 { font-size: 1.9rem; font-weight: 800; margin: 0; letter-spacing: -0.01em; color: white; display: flex; align-items: center; gap: 0.6rem; }
                .page-content { padding: 2rem clamp(1rem, 3vw, 2.5rem) 3.5rem; }
                .table-card {
                  background: var(--surface);
                  border: 1px solid var(--border);
                  border-radius: 12px;
                  box-shadow: var(--shadow);
                  overflow: auto;
                }
                table { border-collapse: collapse; width: 100%; min-width: 960px; }
                th, td { text-align: left; padding: 0.85rem 1.15rem; border-bottom: 1px solid var(--border); }
                td { color: var(--text-primary); font-size: 0.92rem; }
                td:nth-child(3), td:nth-child(5), td:nth-child(6), td:nth-child(9) { font-variant-numeric: tabular-nums; }
                th:nth-child(3), td:nth-child(3),
                th:nth-child(5), td:nth-child(5),
                th:nth-child(9), td:nth-child(9) { text-align: right; }
                .score-drilldown ul, .limit-drilldown ul, .risk-drilldown ul { text-align: left; }
                th {
                  position: sticky; top: 0;
                  background: var(--surface);
                  font-size: 0.74rem; letter-spacing: 0.05em; text-transform: uppercase;
                  color: var(--text-muted); font-weight: 700;
                  border-bottom: 2px solid var(--border);
                  white-space: nowrap;
                }
                th[data-col] { cursor: pointer; user-select: none; }
                th[data-col]:hover { color: var(--accent); }
                .sort-indicator { display: inline-block; width: 1em; color: var(--accent); }
                tbody tr:hover td { background: color-mix(in srgb, var(--accent) 5%, var(--surface)); }
                a { color: var(--accent); text-decoration: none; }
                a:hover { text-decoration: underline; }
                .badge, .score-badge {
                  display: inline-flex; align-items: center; gap: 0.35rem; padding: 0.3rem 0.8rem; border-radius: 999px;
                  font-size: 0.76rem; font-weight: 800; color: white; letter-spacing: 0.03em; text-transform: uppercase;
                }
                .badge::before { content: "●"; font-size: 0.6rem; }
                .badge.high { background: var(--critical); }
                .badge.medium { background: var(--serious); }
                .badge.low { background: var(--warning); color: #3d2c00; }
                .badge.current { background: var(--good); }
                .score-badge.grade-a { background: var(--good); }
                .score-badge.grade-b { background: var(--good); opacity: 0.82; }
                .score-badge.grade-c { background: var(--warning); color: #3d2c00; }
                .score-badge.grade-d { background: var(--serious); }
                .score-badge.grade-f { background: var(--critical); }
                .score-drilldown summary, .limit-drilldown summary, .risk-drilldown summary { cursor: pointer; list-style: none; }
                .score-drilldown summary::-webkit-details-marker, .limit-drilldown summary::-webkit-details-marker, .risk-drilldown summary::-webkit-details-marker { display: none; }
                .score-drilldown ul, .limit-drilldown ul, .risk-drilldown ul { margin: 0.4rem 0 0; padding: 0 0 0 1.1rem; font-size: 0.75rem; color: var(--text-secondary); }
                .score-drilldown li, .limit-drilldown li, .risk-drilldown li { margin: 0.15rem 0; }
                .trend { font-weight: 600; }
                .trend.worsening { color: var(--critical); }
                .trend.improving { color: var(--good); }
                .trend.stable { color: var(--text-muted); font-weight: 500; }
                .muted { color: var(--text-muted); font-style: italic; }
                .limit-exceeded { color: var(--critical); font-weight: 700; }
                .ch-status { font-weight: 600; }
                .ch-status.distressed { color: var(--critical); }
                .ch-status.overdue-filings { color: var(--serious); }
                .ch-status.healthy { color: var(--good); }
                .ch-meta { font-size: 0.75rem; color: var(--text-muted); margin-top: 0.15rem; }
                .ch-meta.warn { color: var(--serious); }
                .concentration { font-weight: 600; }
                .concentration.high { color: var(--critical); }
                .concentration.medium { color: var(--serious); }
                .concentration.low { color: var(--text-muted); font-weight: 500; }
                .benchmark {
                  background: var(--surface); border: 1px solid var(--border); border-radius: 12px;
                  padding: 1.1rem 1.5rem; margin-bottom: 1.75rem; box-shadow: var(--shadow);
                  display: flex; align-items: center; gap: 1.5rem; flex-wrap: wrap;
                }
                .benchmark-figure { display: flex; align-items: baseline; gap: 0.6rem; }
                .benchmark-value { font-size: 1.8rem; font-weight: 800; font-variant-numeric: tabular-nums; }
                .benchmark-value.good { color: var(--good); }
                .benchmark-value.critical { color: var(--critical); }
                .benchmark-value.muted { color: var(--text-muted); }
                .benchmark-label { font-size: 0.85rem; color: var(--text-secondary); }
                .benchmark-compare { font-size: 0.85rem; color: var(--text-secondary); }
                .benchmark-verdict { font-weight: 700; margin-left: 0.4rem; }
                .benchmark-verdict.good { color: var(--good); }
                .benchmark-verdict.critical { color: var(--critical); }
                .benchmark-verdict.muted { color: var(--text-muted); }
                .benchmark-source { font-size: 0.72rem; color: var(--text-muted); width: 100%; }
                .warnings {
                  background: color-mix(in srgb, var(--warning) 12%, var(--surface));
                  border: 1px solid var(--border); border-left: 5px solid var(--warning);
                  border-radius: 12px; padding: 1.1rem 1.5rem; margin-bottom: 1.75rem;
                  box-shadow: var(--shadow);
                }
                .warnings h2 { font-size: 1rem; margin: 0 0 0.4rem; display: flex; align-items: center; justify-content: space-between; color: var(--text-primary); }
                .warnings-toggle-btn {
                  background: var(--surface); border: 1px solid var(--border); color: var(--text-secondary);
                  border-radius: 6px; padding: 0.2rem 0.7rem; font-size: 0.75rem; cursor: pointer; font-weight: 600;
                }
                .warnings-toggle-btn:hover { border-color: var(--warning); color: var(--text-primary); }
                .warnings ul { margin: 0.3rem 0 0.6rem 1.2rem; padding: 0; }
                .warnings li { margin: 0.2rem 0; color: var(--text-secondary); }
                .warnings-list { column-count: 2; column-gap: 2rem; }
                .warnings details { margin: 0.3rem 0; break-inside: avoid; }
                .warnings summary { cursor: pointer; font-weight: 600; padding: 0.2rem 0; color: var(--text-primary); }
                .warnings summary a { font-weight: 600; }
                .warning-count { background: var(--warning); color: #3d2c00; border-radius: 999px; padding: 0.05rem 0.55rem; font-size: 0.75rem; margin-left: 0.4rem; font-weight: 700; }
                .group-toggle { margin-bottom: 0.6rem; font-size: 0.85rem; color: var(--text-secondary); }
                .group-toggle label { margin-right: 1.2rem; cursor: pointer; }
                .live-status {
                  font-size: 0.72rem; font-weight: 700; padding: 0.3rem 0.75rem; border-radius: 999px;
                  vertical-align: middle; letter-spacing: 0.03em;
                  background: rgba(255,255,255,0.16); color: white; border: 1px solid rgba(255,255,255,0.3);
                }
                tbody tr { transition: background-color 1s ease; }
                tbody tr.highlight-updated { background-color: var(--highlight); }
                tbody tr.highlight-updated td { background: var(--highlight); }
              </style>
            </head>
            <body>
              <header class="app-header">
                <div class="app-header-inner">
                  <h1>📊 Credit Risk Dashboard</h1>
                  <span id="liveStatus" class="live-status connecting">connecting…</span>
                </div>
              </header>
              <main class="page-content">
              {{benchmarkSection}}
              {{warningsSection}}
              <div class="table-card">
                <table>
                  <thead>
                    <tr>
                      <th data-col="0">Contact<span class="sort-indicator"></span></th>
                      <th data-col="1">Score<span class="sort-indicator"></span></th>
                      <th data-col="2">Outstanding<span class="sort-indicator"></span></th>
                      <th data-col="3">Concentration<span class="sort-indicator"></span></th>
                      <th data-col="4">Overdue<span class="sort-indicator"></span></th>
                      <th data-col="5">Oldest Overdue (days)<span class="sort-indicator"></span></th>
                      <th data-col="6">Risk<span class="sort-indicator"></span></th>
                      <th data-col="7">Payment Trend<span class="sort-indicator"></span></th>
                      <th data-col="8">Recommended Limit<span class="sort-indicator"></span></th>
                      <th data-col="9">Companies House<span class="sort-indicator"></span></th>
                    </tr>
                  </thead>
                  <tbody>
                    {{rows}}
                  </tbody>
                </table>
              </div>
              </main>

              <script>
                // Apply any highlight requested by the reload that just happened, then keep the
                // remaining time ticking down (the highlight window survives the full page reload
                // via sessionStorage, since the SSE connection itself doesn't).
                (() => {
                  const until = parseInt(sessionStorage.getItem('highlightUntil') || '0', 10);
                  const remaining = until - Date.now();
                  if (remaining <= 0) {
                    sessionStorage.removeItem('highlightUntil');
                    sessionStorage.removeItem('highlightContactIds');
                    return;
                  }

                  const ids = JSON.parse(sessionStorage.getItem('highlightContactIds') || '[]');
                  ids.forEach(id => {
                    const row = document.querySelector(`tr[data-contact-id="${id}"]`);
                    if (row) row.classList.add('highlight-updated');
                  });

                  setTimeout(() => {
                    document.querySelectorAll('.highlight-updated').forEach(row => row.classList.remove('highlight-updated'));
                    sessionStorage.removeItem('highlightUntil');
                    sessionStorage.removeItem('highlightContactIds');
                  }, remaining);
                })();

                const liveStatus = document.getElementById('liveStatus');
                const source = new EventSource('/dashboard/events');

                source.onopen = () => {
                  liveStatus.textContent = '🟢 Live';
                  liveStatus.className = 'live-status live';
                };
                source.onerror = () => {
                  liveStatus.textContent = '🔴 Disconnected';
                  liveStatus.className = 'live-status disconnected';
                };
                source.onmessage = (e) => {
                  const data = JSON.parse(e.data);
                  sessionStorage.setItem('highlightContactIds', JSON.stringify(data.contactIds || []));
                  sessionStorage.setItem('highlightUntil', String(Date.now() + 15000));
                  location.reload();
                };

                document.querySelectorAll('input[name="groupBy"]').forEach(radio => {
                  radio.addEventListener('change', (e) => {
                    document.getElementById('warningsByContact').style.display = e.target.value === 'contact' ? '' : 'none';
                    document.getElementById('warningsByType').style.display = e.target.value === 'type' ? '' : 'none';
                  });
                });

                // Collapse state survives the frequent auto-reloads (sessionStorage), so toggling it
                // doesn't get undone the next time a webhook triggers a refresh.
                const warningsToggle = document.getElementById('warningsToggle');
                if (warningsToggle) {
                  const warningsContent = document.getElementById('warningsContent');

                  const setCollapsed = (collapsed) => {
                    warningsContent.style.display = collapsed ? 'none' : '';
                    warningsToggle.textContent = collapsed ? '▶ Expand' : '▼ Collapse';
                    sessionStorage.setItem('warningsCollapsed', collapsed ? '1' : '0');
                  };

                  // Defaults to collapsed on a fresh session; an explicit prior choice (including
                  // "expanded") is respected on subsequent reloads.
                  setCollapsed(sessionStorage.getItem('warningsCollapsed') !== '0');

                  warningsToggle.addEventListener('click', () => {
                    setCollapsed(warningsContent.style.display !== 'none');
                  });
                }

                // Column sorting: reads the raw value from each cell's data-sort-value (kept separate
                // from the formatted display text), sorting missing/non-numeric values to the bottom
                // regardless of direction, and numeric vs. text columns compared appropriately.
                (() => {
                  const tbody = document.querySelector('table tbody');
                  const headers = document.querySelectorAll('th[data-col]');
                  let sortState = { col: null, dir: 1 };

                  headers.forEach(th => {
                    th.addEventListener('click', () => {
                      const col = parseInt(th.dataset.col, 10);
                      const dir = (sortState.col === col) ? -sortState.dir : 1;
                      sortState = { col, dir };

                      headers.forEach(h => h.querySelector('.sort-indicator').textContent = '');
                      th.querySelector('.sort-indicator').textContent = dir === 1 ? '▲' : '▼';

                      const rows = Array.from(tbody.querySelectorAll('tr'));
                      rows.sort((a, b) => {
                        const aCell = a.children[col];
                        const bCell = b.children[col];
                        const aRaw = aCell.dataset.sortValue ?? aCell.textContent.trim();
                        const bRaw = bCell.dataset.sortValue ?? bCell.textContent.trim();

                        const aNum = parseFloat(aRaw);
                        const bNum = parseFloat(bRaw);
                        const aIsNum = aRaw !== '' && !isNaN(aNum);
                        const bIsNum = bRaw !== '' && !isNaN(bNum);

                        if (aIsNum && bIsNum) return (aNum - bNum) * dir;
                        if (aIsNum !== bIsNum) return aIsNum ? -1 : 1;
                        return aRaw.localeCompare(bRaw) * dir;
                      });

                      rows.forEach(row => tbody.appendChild(row));
                    });
                  });
                })();
              </script>
            </body>
            </html>
            """;

        return Content(html, "text/html; charset=utf-8");
    }

    private static string TrendLabel(double delta) => delta switch
    {
        > 2 => "▲ Worsening",
        < -2 => "▼ Improving",
        _ => "▬ Stable"
    };

    private static string TrendClass(double delta) => delta switch
    {
        > 2 => "worsening",
        < -2 => "improving",
        _ => "stable"
    };

    private static string ConcentrationClass(decimal percent) => percent switch
    {
        > 25 => "high",
        > 10 => "medium",
        _ => "low"
    };

    private static string GradeClass(string grade) => $"grade-{grade.ToLowerInvariant()}";

    /// <summary>Numeric rank for sorting the Companies House column: worse signals sort first.</summary>
    private static int CompaniesHouseSortRank(ContactCreditRisk r) => r switch
    {
        { CompaniesHouseDistressed: true } => 0,
        { CompaniesHouseOverdueFilings: true } => 1,
        { CompaniesHouseStatus: not null } => 2,
        _ => 3
    };

    private static string WarningTypeLabel(EarlyWarningType type) => type switch
    {
        EarlyWarningType.FirstLatePayment => "First Late Payment",
        EarlyWarningType.AcceleratingLateness => "Accelerating Lateness",
        EarlyWarningType.ExceedsRecommendedLimit => "Exceeds Recommended Limit",
        EarlyWarningType.CompanyDistressSignal => "Company Distress Signal",
        EarlyWarningType.PriorInsolvencyHistory => "Prior Insolvency History",
        EarlyWarningType.ConcentrationRisk => "Concentration Risk",
        _ => type.ToString()
    };

    /// <summary>UK SME invoices currently overdue, per Sage/CEBR analysis of 1.2M+ real invoices (2026) — see docs/uk-sme-late-payment-cost-research.md.</summary>
    private const decimal NationalOverdueInvoicePercent = 49m;

    private static string BuildBenchmarkSection(int overdueInvoices, int outstandingInvoices)
    {
        var orgPercent = Math.Round((decimal)overdueInvoices / outstandingInvoices * 100, 1);
        var diff = orgPercent - NationalOverdueInvoicePercent;

        var (statusClass, verdict) = diff switch
        {
            < -1 => ("good", "✓ Better than the UK average"),
            > 1 => ("critical", "⚠ Worse than the UK average"),
            _ => ("muted", "— About average")
        };

        return $"""
            <div class="benchmark">
              <div class="benchmark-figure">
                <span class="benchmark-value {statusClass}">{orgPercent:0.#}%</span>
                <span class="benchmark-label">of your outstanding invoices are currently overdue</span>
              </div>
              <div class="benchmark-compare">
                UK SME average: <strong>{NationalOverdueInvoicePercent:0}%</strong>
                <span class="benchmark-verdict {statusClass}">{verdict}</span>
              </div>
              <div class="benchmark-source">Source: Sage/CEBR analysis of 1.2M+ UK invoices (2026)</div>
            </div>
            """;
    }
}
