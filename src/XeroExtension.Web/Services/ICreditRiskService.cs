using XeroExtension.Web.Models;

namespace XeroExtension.Web.Services;

public interface ICreditRiskService
{
    /// <summary>Ranks customers by payment risk, based on their overdue sales invoices in Xero.</summary>
    Task<List<ContactCreditRisk>> GetContactRiskAsync(string tenantId);

    /// <summary>Writes the current risk assessment back to Xero as "Risk: High/Medium/Low" Contact Groups.</summary>
    Task SyncRiskGroupsToXeroAsync(string tenantId);
}
