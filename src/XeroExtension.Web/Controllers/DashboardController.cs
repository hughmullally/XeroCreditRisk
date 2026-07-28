using System.Net;
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

        var changed = new TaskCompletionSource();
        void OnChanged() => changed.TrySetResult();
        _notifier.Changed += OnChanged;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var finished = await Task.WhenAny(changed.Task, Task.Delay(TimeSpan.FromSeconds(25), cancellationToken));

                if (finished == changed.Task)
                {
                    await Response.WriteAsync("data: changed\n\n", cancellationToken);
                    changed = new TaskCompletionSource();
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
            return Content("<p>Missing required query parameter: tenantId</p>", "text/html");

        var risk = await _creditRiskService.GetContactRiskAsync(tenantId);
        var trends = await _creditRiskService.GetPaymentTrendAsync(tenantId);
        var trendByContact = trends.ToDictionary(t => t.ContactId);
        var recommendations = await _creditRiskService.GetCreditLimitRecommendationsAsync(tenantId);
        var recommendationByContact = recommendations.ToDictionary(r => r.ContactId);
        var warnings = await _creditRiskService.GetEarlyWarningsAsync(tenantId);

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
              <h2>⚠ Early Warnings ({warnings.Count} across {warningsByContact.Count} counterparties)</h2>
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
            """;

        var rows = string.Join("\n", risk.Select(r =>
        {
            var trendCell = trendByContact.TryGetValue(r.ContactId, out var trend)
                ? $"""<span class="trend {TrendClass(trend.TrendDelta)}">{Math.Round(trend.AverageDaysLate, 1)} days avg {TrendLabel(trend.TrendDelta)}</span>"""
                : """<span class="muted">No payment history</span>""";

            var limitCell = recommendationByContact.TryGetValue(r.ContactId, out var rec)
                ? $"""
                    <span class="{(rec.ExceedsRecommendedLimit ? "limit-exceeded" : "")}" title="{WebUtility.HtmlEncode(rec.Rationale)}">
                      {rec.RecommendedCreditLimit:C}{(rec.ExceedsRecommendedLimit ? " ⚠" : "")}
                    </span>
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

            return $"""
                <tr>
                  <td><a href="https://go.xero.com/Contacts/Edit.aspx?contactID={r.ContactId}" target="_blank">{WebUtility.HtmlEncode(r.ContactName)}</a></td>
                  <td>{r.OutstandingAmount:C}</td>
                  <td>{concentrationCell}</td>
                  <td>{r.OverdueAmount:C}</td>
                  <td>{r.OldestOverdueDays}</td>
                  <td><span class="badge {r.RiskLevel.ToString().ToLowerInvariant()}">{r.RiskLevel}</span></td>
                  <td>{trendCell}</td>
                  <td>{limitCell}</td>
                  <td>{chCell}</td>
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
                a { color: #13b5ea; text-decoration: none; }
                a:hover { text-decoration: underline; }
                .badge { padding: 0.2rem 0.6rem; border-radius: 999px; font-size: 0.8rem; font-weight: 600; color: white; }
                .badge.high { background: #d64545; }
                .badge.medium { background: #e0a030; }
                .badge.low { background: #3ba55c; }
                .badge.current { background: #6c757d; }
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
                .warnings h2 { font-size: 1rem; margin: 0 0 0.4rem; }
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
              </style>
            </head>
            <body>
              <h1>Credit Risk Dashboard <span id="liveStatus" class="live-status connecting">connecting…</span></h1>
              {{warningsSection}}
              <table>
                <thead>
                  <tr><th>Contact</th><th>Outstanding</th><th>Concentration</th><th>Overdue</th><th>Oldest Overdue (days)</th><th>Risk</th><th>Payment Trend</th><th>Recommended Limit</th><th>Companies House</th></tr>
                </thead>
                <tbody>
                  {{rows}}
                </tbody>
              </table>

              <script>
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
                source.onmessage = () => location.reload();

                document.querySelectorAll('input[name="groupBy"]').forEach(radio => {
                  radio.addEventListener('change', (e) => {
                    document.getElementById('warningsByContact').style.display = e.target.value === 'contact' ? '' : 'none';
                    document.getElementById('warningsByType').style.display = e.target.value === 'type' ? '' : 'none';
                  });
                });
              </script>
            </body>
            </html>
            """;

        return Content(html, "text/html");
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
