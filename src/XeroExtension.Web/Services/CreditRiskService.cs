using Xero.NetStandard.OAuth2.Model.Accounting;
using XeroExtension.Web.Models;

namespace XeroExtension.Web.Services;

public class CreditRiskService(IXeroService xeroService) : ICreditRiskService
{
    private static readonly IReadOnlyDictionary<CreditRiskLevel, string> RiskGroupNames = new Dictionary<CreditRiskLevel, string>
    {
        [CreditRiskLevel.High] = "Risk: High",
        [CreditRiskLevel.Medium] = "Risk: Medium",
        [CreditRiskLevel.Low] = "Risk: Low",
        [CreditRiskLevel.Current] = "Risk: Current"
    };

    public async Task SyncRiskGroupsToXeroAsync(string tenantId)
    {
        var risk = await GetContactRiskAsync(tenantId);

        foreach (var (level, groupName) in RiskGroupNames)
        {
            var groupId = await xeroService.EnsureContactGroupAsync(tenantId, groupName);
            var contactIds = risk.Where(r => r.RiskLevel == level).Select(r => r.ContactId).ToList();
            await xeroService.ReplaceContactGroupMembersAsync(tenantId, groupId, contactIds);
        }
    }

    public async Task<List<ContactCreditRisk>> GetContactRiskAsync(string tenantId)
    {
        var invoices = await xeroService.GetInvoicesAsync(tenantId);
        var now = DateTime.UtcNow;

        var outstanding = invoices.Where(i =>
            i.Type == Invoice.TypeEnum.ACCREC &&
            i.Status == Invoice.StatusEnum.AUTHORISED &&
            i.AmountDue > 0);

        return outstanding
            .GroupBy(i => i.Contact.ContactID)
            .Select(g =>
            {
                var overdue = g.Where(i => i.DueDate < now).ToList();
                var oldestOverdueDays = overdue.Count > 0 ? overdue.Max(i => (now - i.DueDate!.Value).Days) : 0;

                return new ContactCreditRisk
                {
                    ContactId = g.Key.ToString() ?? string.Empty,
                    ContactName = g.First().Contact.Name,
                    OutstandingInvoiceCount = g.Count(),
                    OutstandingAmount = g.Sum(i => i.AmountDue ?? 0),
                    OverdueInvoiceCount = overdue.Count,
                    OverdueAmount = overdue.Sum(i => i.AmountDue ?? 0),
                    OldestOverdueDays = oldestOverdueDays,
                    RiskLevel = overdue.Count == 0
                        ? CreditRiskLevel.Current
                        : oldestOverdueDays switch
                        {
                            >= 60 => CreditRiskLevel.High,
                            >= 30 => CreditRiskLevel.Medium,
                            _ => CreditRiskLevel.Low
                        }
                };
            })
            .OrderByDescending(r => r.OldestOverdueDays)
            .ToList();
    }

    public async Task<List<ContactPaymentTrend>> GetPaymentTrendAsync(string tenantId)
    {
        var invoices = await xeroService.GetInvoicesAsync(tenantId);

        var paid = invoices.Where(i =>
            i.Type == Invoice.TypeEnum.ACCREC &&
            i.Status == Invoice.StatusEnum.PAID &&
            i.DueDate is not null &&
            i.FullyPaidOnDate is not null);

        return paid
            .GroupBy(i => i.Contact.ContactID)
            .Select(g =>
            {
                var history = g
                    .OrderBy(i => i.DueDate)
                    .Select(i => new PaymentHistoryEntry
                    {
                        InvoiceNumber = i.InvoiceNumber,
                        DueDate = i.DueDate!.Value,
                        PaidDate = i.FullyPaidOnDate!.Value,
                        DaysLate = (i.FullyPaidOnDate!.Value - i.DueDate!.Value).Days
                    })
                    .ToList();

                var half = history.Count / 2;
                var trendDelta = half > 0
                    ? history.Skip(history.Count - half).Average(h => h.DaysLate)
                      - history.Take(half).Average(h => h.DaysLate)
                    : 0;

                return new ContactPaymentTrend
                {
                    ContactId = g.Key.ToString() ?? string.Empty,
                    ContactName = g.First().Contact.Name,
                    History = history,
                    AverageDaysLate = history.Average(h => h.DaysLate),
                    TrendDelta = trendDelta
                };
            })
            .OrderByDescending(r => r.TrendDelta)
            .ToList();
    }

    public async Task<List<CreditLimitRecommendation>> GetCreditLimitRecommendationsAsync(string tenantId)
    {
        var invoices = await xeroService.GetInvoicesAsync(tenantId);
        var riskByContact = (await GetContactRiskAsync(tenantId)).ToDictionary(r => r.ContactId);
        var trendByContact = (await GetPaymentTrendAsync(tenantId)).ToDictionary(t => t.ContactId);

        var salesInvoices = invoices.Where(i =>
            i.Type == Invoice.TypeEnum.ACCREC &&
            i.Status is Invoice.StatusEnum.AUTHORISED or Invoice.StatusEnum.PAID &&
            i.Total > 0);

        return salesInvoices
            .GroupBy(i => i.Contact.ContactID)
            .Select(g =>
            {
                var contactId = g.Key.ToString() ?? string.Empty;
                var avgInvoiceAmount = g.Average(i => i.Total!.Value);

                riskByContact.TryGetValue(contactId, out var risk);
                trendByContact.TryGetValue(contactId, out var trend);

                var riskLevel = risk?.RiskLevel ?? CreditRiskLevel.Current;
                var trendDelta = trend?.TrendDelta ?? 0;

                var multiplier = riskLevel switch
                {
                    CreditRiskLevel.Current => 3.0,
                    CreditRiskLevel.Low => 2.0,
                    CreditRiskLevel.Medium => 1.0,
                    _ => 0.5
                };
                if (trendDelta > 2) multiplier *= 0.8;
                else if (trendDelta < -2) multiplier *= 1.1;

                var trendNote = trendDelta > 2 ? ", worsening payment trend"
                    : trendDelta < -2 ? ", improving payment trend"
                    : "";

                return new CreditLimitRecommendation
                {
                    ContactId = contactId,
                    ContactName = g.First().Contact.Name,
                    AverageInvoiceAmount = Math.Round(avgInvoiceAmount, 2),
                    CurrentOutstanding = risk?.OutstandingAmount ?? 0,
                    RiskLevel = riskLevel,
                    RecommendedCreditLimit = Math.Ceiling(avgInvoiceAmount * (decimal)multiplier / 100) * 100,
                    Rationale = $"Avg invoice {avgInvoiceAmount:C}, {riskLevel} risk{trendNote}."
                };
            })
            .OrderBy(r => r.ContactName)
            .ToList();
    }

    public async Task<List<EarlyWarningTrigger>> GetEarlyWarningsAsync(string tenantId)
    {
        var trends = await GetPaymentTrendAsync(tenantId);
        var recommendations = await GetCreditLimitRecommendationsAsync(tenantId);

        var warnings = new List<EarlyWarningTrigger>();

        foreach (var trend in trends)
        {
            if (trend.History.Count >= 2)
            {
                var mostRecent = trend.History[^1];
                var priorHistory = trend.History[..^1];

                if (mostRecent.DaysLate > 0 && priorHistory.All(h => h.DaysLate <= 0))
                {
                    warnings.Add(new EarlyWarningTrigger
                    {
                        ContactId = trend.ContactId,
                        ContactName = trend.ContactName,
                        Type = EarlyWarningType.FirstLatePayment,
                        Message = $"Always paid on time before, but invoice {mostRecent.InvoiceNumber} was {mostRecent.DaysLate} days late — first late payment on record."
                    });
                }
            }

            if (trend.TrendDelta > 7)
            {
                warnings.Add(new EarlyWarningTrigger
                {
                    ContactId = trend.ContactId,
                    ContactName = trend.ContactName,
                    Type = EarlyWarningType.AcceleratingLateness,
                    Message = $"Payment lateness has worsened by {trend.TrendDelta:0.0} days recently — accelerating even though not yet high risk."
                });
            }
        }

        foreach (var rec in recommendations.Where(r => r.ExceedsRecommendedLimit))
        {
            warnings.Add(new EarlyWarningTrigger
            {
                ContactId = rec.ContactId,
                ContactName = rec.ContactName,
                Type = EarlyWarningType.ExceedsRecommendedLimit,
                Message = $"Currently owes {rec.CurrentOutstanding:C}, above the recommended {rec.RecommendedCreditLimit:C} limit."
            });
        }

        return warnings
            .OrderBy(w => w.ContactName)
            .ThenBy(w => w.Type)
            .ToList();
    }
}
