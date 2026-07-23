using System.Net;
using Microsoft.AspNetCore.Mvc;
using XeroExtension.Web.Services;

namespace XeroExtension.Web.Controllers;

[Route("dashboard")]
public class DashboardController : ControllerBase
{
    private readonly ICreditRiskService _creditRiskService;

    public DashboardController(ICreditRiskService creditRiskService) => _creditRiskService = creditRiskService;

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

            return $"""
                <tr>
                  <td><a href="https://go.xero.com/Contacts/Edit.aspx?contactID={r.ContactId}" target="_blank">{WebUtility.HtmlEncode(r.ContactName)}</a></td>
                  <td>{r.OutstandingAmount:C}</td>
                  <td>{r.OverdueAmount:C}</td>
                  <td>{r.OldestOverdueDays}</td>
                  <td><span class="badge {r.RiskLevel.ToString().ToLowerInvariant()}">{r.RiskLevel}</span></td>
                  <td>{trendCell}</td>
                  <td>{limitCell}</td>
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
              </style>
            </head>
            <body>
              <h1>Credit Risk Dashboard</h1>
              <table>
                <thead>
                  <tr><th>Contact</th><th>Outstanding</th><th>Overdue</th><th>Oldest Overdue (days)</th><th>Risk</th><th>Payment Trend</th><th>Recommended Limit</th></tr>
                </thead>
                <tbody>
                  {{rows}}
                </tbody>
              </table>
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
}
