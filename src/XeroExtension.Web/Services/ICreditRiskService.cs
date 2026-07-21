using XeroExtension.Web.Models;

namespace XeroExtension.Web.Services;

public interface ICreditRiskService
{
    /// <summary>Ranks customers by payment risk, based on their overdue sales invoices in Xero.</summary>
    Task<List<ContactCreditRisk>> GetContactRiskAsync(string tenantId);
}
