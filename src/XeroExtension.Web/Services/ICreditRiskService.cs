using XeroExtension.Web.Models;

namespace XeroExtension.Web.Services;

public interface ICreditRiskService
{
    /// <summary>Ranks customers by payment risk, based on their overdue sales invoices in Xero.</summary>
    Task<List<ContactCreditRisk>> GetContactRiskAsync(string tenantId);

    /// <summary>Writes the current risk assessment back to Xero as "Risk: High/Medium/Low" Contact Groups.</summary>
    Task SyncRiskGroupsToXeroAsync(string tenantId);

    /// <summary>Per-contact history of how late paid invoices were versus their due date, plus a trend direction.</summary>
    Task<List<ContactPaymentTrend>> GetPaymentTrendAsync(string tenantId);

    /// <summary>
    /// Suggests a credit limit per contact based on their typical invoice size, scaled by their
    /// current risk tier and payment trend. Xero has no native credit limit field on Contacts, so
    /// this is a computed recommendation only — it isn't written back to Xero.
    /// </summary>
    Task<List<CreditLimitRecommendation>> GetCreditLimitRecommendationsAsync(string tenantId);
}
