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
                          {rec.RecommendedCreditLimit:C}{(rec.ExceedsRecommendedLimit ? " ⚠" : "")}
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
                  <td data-sort-value="{r.OutstandingAmount}">{r.OutstandingAmount:C}</td>
                  <td data-sort-value="{r.ConcentrationPercent}">{concentrationCell}</td>
                  <td data-sort-value="{r.OverdueAmount}">{r.OverdueAmount:C}</td>
                  <td data-sort-value="{r.OldestOverdueDays}">{r.OldestOverdueDays}</td>
                  <td data-sort-value="{(int)r.RiskLevel}"><span class="badge {r.RiskLevel.ToString().ToLowerInvariant()}">{r.RiskLevel}</span></td>
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
                body { font-family: -apple-system, "Segoe UI", sans-serif; margin: 2rem; background: #f7f7f8; color: #222; }
                h1 { font-size: 1.4rem; }
                table { border-collapse: collapse; width: 100%; background: white; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
                th, td { text-align: left; padding: 0.6rem 1rem; border-bottom: 1px solid #eee; }
                th { background: #fafafa; font-size: 0.8rem; text-transform: uppercase; color: #666; }
                th[data-col] { cursor: pointer; user-select: none; }
                th[data-col]:hover { color: #222; }
                .sort-indicator { display: inline-block; width: 1em; color: #13b5ea; }
                a { color: #13b5ea; text-decoration: none; }
                a:hover { text-decoration: underline; }
                .badge { padding: 0.2rem 0.6rem; border-radius: 999px; font-size: 0.8rem; font-weight: 600; color: white; }
                .badge.high { background: #d64545; }
                .badge.medium { background: #e0a030; }
                .badge.low { background: #3ba55c; }
                .badge.current { background: #6c757d; }
                .score-badge { padding: 0.2rem 0.6rem; border-radius: 999px; font-size: 0.8rem; font-weight: 700; color: white; }
                .score-badge.grade-a { background: #3ba55c; }
                .score-badge.grade-b { background: #6cbf6c; }
                .score-badge.grade-c { background: #e0a030; }
                .score-badge.grade-d { background: #e0763a; }
                .score-badge.grade-f { background: #d64545; }
                .score-drilldown summary, .limit-drilldown summary { cursor: pointer; list-style: none; }
                .score-drilldown summary::-webkit-details-marker, .limit-drilldown summary::-webkit-details-marker { display: none; }
                .score-drilldown ul, .limit-drilldown ul { margin: 0.4rem 0 0; padding: 0 0 0 1.1rem; font-size: 0.75rem; color: #555; }
                .score-drilldown li, .limit-drilldown li { margin: 0.1rem 0; }
                .trend { font-weight: 600; }
                .trend.worsening { color: #d64545; }
                .trend.improving { color: #3ba55c; }
                .trend.stable { color: #888; }
                .muted { color: #999; font-style: italic; }
                .limit-exceeded { color: #d64545; font-weight: 600; }
                .ch-status { font-weight: 600; }
                .ch-status.distressed { color: #d64545; }
                .ch-status.overdue-filings { color: #e0a030; }
                .ch-status.healthy { color: #3ba55c; }
                .ch-meta { font-size: 0.75rem; color: #888; margin-top: 0.15rem; }
                .ch-meta.warn { color: #e0a030; }
                .concentration { font-weight: 600; }
                .concentration.high { color: #d64545; }
                .concentration.medium { color: #e0a030; }
                .concentration.low { color: #888; font-weight: normal; }
                .warnings { background: #fff8e6; border-left: 4px solid #e0a030; border-radius: 4px; padding: 0.8rem 1.2rem; margin-bottom: 1.5rem; }
                .warnings h2 { font-size: 1rem; margin: 0 0 0.4rem; display: flex; align-items: center; justify-content: space-between; }
                .warnings-toggle-btn { background: none; border: 1px solid #e0a030; color: #a6720f; border-radius: 4px; padding: 0.15rem 0.6rem; font-size: 0.75rem; cursor: pointer; }
                .warnings-toggle-btn:hover { background: #f5deb0; }
                .warnings ul { margin: 0.3rem 0 0.6rem 1.2rem; padding: 0; }
                .warnings li { margin: 0.2rem 0; }
                .warnings-list { column-count: 2; column-gap: 2rem; }
                .warnings details { margin: 0.3rem 0; break-inside: avoid; }
                .warnings summary { cursor: pointer; font-weight: 600; padding: 0.2rem 0; }
                .warnings summary a { font-weight: 600; }
                .warning-count { background: #e0a030; color: white; border-radius: 999px; padding: 0.05rem 0.55rem; font-size: 0.75rem; margin-left: 0.4rem; }
                .group-toggle { margin-bottom: 0.6rem; font-size: 0.85rem; }
                .group-toggle label { margin-right: 1.2rem; cursor: pointer; }
                .live-status { font-size: 0.7rem; font-weight: 600; padding: 0.15rem 0.5rem; border-radius: 999px; vertical-align: middle; }
                .live-status.connecting { background: #eee; color: #888; }
                .live-status.live { background: #e6f7ec; color: #3ba55c; }
                .live-status.disconnected { background: #fbe7e7; color: #d64545; }
                tbody tr { transition: background-color 1s ease; }
                tbody tr.highlight-updated { background-color: #fff3b0; }
              </style>
            </head>
            <body>
              <h1>Credit Risk Dashboard <span id="liveStatus" class="live-status connecting">connecting…</span></h1>
              {{warningsSection}}
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
}
