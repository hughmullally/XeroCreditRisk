using Xero.NetStandard.OAuth2.Model.Accounting;
using XeroExtension.Web.Models;

namespace XeroExtension.Web.Services;

public class CreditRiskService(IXeroService xeroService) : ICreditRiskService
{
    private static readonly IReadOnlyDictionary<CreditRiskLevel, string> RiskGroupNames = new Dictionary<CreditRiskLevel, string>
    {
        [CreditRiskLevel.High] = "Risk: High",
        [CreditRiskLevel.Medium] = "Risk: Medium",
        [CreditRiskLevel.Low] = "Risk: Low"
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

        var overdue = invoices.Where(i =>
            i.Type == Invoice.TypeEnum.ACCREC &&
            i.Status == Invoice.StatusEnum.AUTHORISED &&
            i.DueDate < now &&
            i.AmountDue > 0);

        return overdue
            .GroupBy(i => i.Contact.ContactID)
            .Select(g =>
            {
                var oldestOverdueDays = g.Max(i => (now - i.DueDate!.Value).Days);
                return new ContactCreditRisk
                {
                    ContactId = g.Key.ToString() ?? string.Empty,
                    ContactName = g.First().Contact.Name,
                    OverdueInvoiceCount = g.Count(),
                    OverdueAmount = g.Sum(i => i.AmountDue ?? 0),
                    OldestOverdueDays = oldestOverdueDays,
                    RiskLevel = oldestOverdueDays switch
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
}
