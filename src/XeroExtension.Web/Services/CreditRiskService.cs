using Xero.NetStandard.OAuth2.Model.Accounting;
using XeroExtension.Web.Models;

namespace XeroExtension.Web.Services;

public class CreditRiskService(IXeroService xeroService, ICompaniesHouseService companiesHouseService) : ICreditRiskService
{
    private static readonly IReadOnlyDictionary<CreditRiskLevel, string> RiskGroupNames = new Dictionary<CreditRiskLevel, string>
    {
        [CreditRiskLevel.High] = "Risk: High",
        [CreditRiskLevel.Medium] = "Risk: Medium",
        [CreditRiskLevel.Low] = "Risk: Low",
        [CreditRiskLevel.Current] = "Risk: Current"
    };

    // This service is Scoped (one instance per HTTP request), so caching here is safely bounded to a
    // single dashboard load — without it, GetCreditLimitRecommendationsAsync and GetEarlyWarningsAsync
    // each independently re-run GetContactRiskAsync (and its Companies House lookups) from scratch,
    // tripling the external API calls on every single page render.
    private List<Invoice>? _cachedInvoices;
    private List<ContactCreditRisk>? _cachedRisk;
    private List<ContactPaymentTrend>? _cachedTrend;

    private async Task<List<Invoice>> GetInvoicesCachedAsync(string tenantId) =>
        _cachedInvoices ??= await xeroService.GetInvoicesAsync(tenantId);

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
        if (_cachedRisk is not null)
            return _cachedRisk;

        var invoices = await GetInvoicesCachedAsync(tenantId);
        var now = DateTime.UtcNow;

        var outstanding = invoices.Where(i =>
            i.Type == Invoice.TypeEnum.ACCREC &&
            i.Status == Invoice.StatusEnum.AUTHORISED &&
            i.AmountDue > 0);

        var grouped = outstanding.GroupBy(i => i.Contact.ContactID).ToList();
        if (grouped.Count == 0)
            return _cachedRisk = [];

        var contacts = await xeroService.GetContactsAsync(tenantId);
        var companyNumberByContact = contacts
            .Where(c => c.ContactID is not null && !string.IsNullOrWhiteSpace(c.CompanyNumber))
            .ToDictionary(c => c.ContactID!.Value, c => c.CompanyNumber!);

        var companyNumbers = grouped
            .Where(g => g.Key.HasValue && companyNumberByContact.ContainsKey(g.Key.Value))
            .Select(g => companyNumberByContact[g.Key!.Value])
            .Distinct()
            .ToList();

        var profileLookups = companyNumbers.ToDictionary(n => n, n => companiesHouseService.GetCompanyProfileAsync(n));
        await Task.WhenAll(profileLookups.Values);
        var profileByCompanyNumber = profileLookups.ToDictionary(kv => kv.Key, kv => kv.Value.Result);

        var results = grouped
            .Select(g =>
            {
                var overdue = g.Where(i => i.DueDate < now).ToList();
                var oldestOverdueDays = overdue.Count > 0 ? overdue.Max(i => (now - i.DueDate!.Value).Days) : 0;

                var riskLevel = overdue.Count == 0
                    ? CreditRiskLevel.Current
                    : oldestOverdueDays switch
                    {
                        >= 60 => CreditRiskLevel.High,
                        >= 30 => CreditRiskLevel.Medium,
                        _ => CreditRiskLevel.Low
                    };

                var companyNumber = g.Key.HasValue && companyNumberByContact.TryGetValue(g.Key.Value, out var cn) ? cn : null;
                var profile = companyNumber is not null && profileByCompanyNumber.TryGetValue(companyNumber, out var p) ? p : null;

                if (profile?.IsDistressed == true)
                    riskLevel = CreditRiskLevel.High;

                return new ContactCreditRisk
                {
                    ContactId = g.Key.ToString() ?? string.Empty,
                    ContactName = g.First().Contact.Name,
                    OutstandingInvoiceCount = g.Count(),
                    OutstandingAmount = g.Sum(i => i.AmountDue ?? 0),
                    OverdueInvoiceCount = overdue.Count,
                    OverdueAmount = overdue.Sum(i => i.AmountDue ?? 0),
                    OldestOverdueDays = oldestOverdueDays,
                    RiskLevel = riskLevel,
                    CompanyNumber = companyNumber,
                    CompaniesHouseStatus = profile?.Status,
                    CompaniesHouseDistressed = profile?.IsDistressed ?? false,
                    CompaniesHouseOverdueFilings = profile is { AccountsOverdue: true } or { ConfirmationStatementOverdue: true },
                    CompanyIncorporationDate = profile?.IncorporationDate,
                    CompaniesHouseHasInsolvencyHistory = profile?.HasInsolvencyHistory ?? false,
                    CompaniesHouseHasCharges = profile?.HasCharges ?? false
                };
            })
            .ToList();

        var totalOutstanding = results.Sum(r => r.OutstandingAmount);
        if (totalOutstanding > 0)
        {
            foreach (var r in results)
                r.ConcentrationPercent = Math.Round(r.OutstandingAmount / totalOutstanding * 100, 1);
        }

        _cachedRisk = results
            .OrderByDescending(r => r.OldestOverdueDays)
            .ToList();
        return _cachedRisk;
    }

    public async Task<List<ContactPaymentTrend>> GetPaymentTrendAsync(string tenantId)
    {
        if (_cachedTrend is not null)
            return _cachedTrend;

        var invoices = await GetInvoicesCachedAsync(tenantId);

        var paid = invoices.Where(i =>
            i.Type == Invoice.TypeEnum.ACCREC &&
            i.Status == Invoice.StatusEnum.PAID &&
            i.DueDate is not null &&
            i.FullyPaidOnDate is not null);

        _cachedTrend = paid
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
        return _cachedTrend;
    }

    public async Task<List<CreditLimitRecommendation>> GetCreditLimitRecommendationsAsync(string tenantId)
    {
        var invoices = await GetInvoicesCachedAsync(tenantId);
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
        var risk = await GetContactRiskAsync(tenantId);
        var trends = await GetPaymentTrendAsync(tenantId);
        var recommendations = await GetCreditLimitRecommendationsAsync(tenantId);

        var warnings = new List<EarlyWarningTrigger>();

        foreach (var r in risk)
        {
            if (r.CompaniesHouseDistressed)
            {
                warnings.Add(new EarlyWarningTrigger
                {
                    ContactId = r.ContactId,
                    ContactName = r.ContactName,
                    Type = EarlyWarningType.CompanyDistressSignal,
                    Message = $"Companies House shows this company's status as \"{r.CompaniesHouseStatus}\"."
                });
            }
            else if (r.CompaniesHouseOverdueFilings)
            {
                warnings.Add(new EarlyWarningTrigger
                {
                    ContactId = r.ContactId,
                    ContactName = r.ContactName,
                    Type = EarlyWarningType.CompanyDistressSignal,
                    Message = "Companies House shows overdue statutory filings (accounts or confirmation statement)."
                });
            }

            if (r.CompaniesHouseHasInsolvencyHistory)
            {
                warnings.Add(new EarlyWarningTrigger
                {
                    ContactId = r.ContactId,
                    ContactName = r.ContactName,
                    Type = EarlyWarningType.PriorInsolvencyHistory,
                    Message = "Companies House shows this company has been through insolvency proceedings before, even though it's currently trading."
                });
            }

            if (r.ConcentrationPercent > 25)
            {
                warnings.Add(new EarlyWarningTrigger
                {
                    ContactId = r.ContactId,
                    ContactName = r.ContactName,
                    Type = EarlyWarningType.ConcentrationRisk,
                    Message = $"Accounts for {r.ConcentrationPercent:0.#}% of total outstanding receivables — a large share to have riding on one contact."
                });
            }
        }

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
