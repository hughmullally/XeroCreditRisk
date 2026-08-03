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

    /// <summary>
    /// Flags contacts showing early signs of trouble that wouldn't yet show up in their current
    /// risk tier alone: a reliable payer's first-ever late payment, lateness accelerating faster
    /// than their risk tier reflects, or already exceeding their recommended credit limit.
    /// </summary>
    Task<List<EarlyWarningTrigger>> GetEarlyWarningsAsync(string tenantId);

    /// <summary>
    /// A single 0-100 score (higher = safer) and A-F grade per contact, combining overdue severity,
    /// payment trend, concentration, credit limit breach, and Companies House signals into one
    /// sortable number rather than several separate badges.
    /// </summary>
    Task<List<ContactCreditScore>> GetCreditScoresAsync(string tenantId);

    /// <summary>
    /// Buckets every outstanding sales invoice by age (not yet due, 1-30, 31-60, 61-90, 90+ days
    /// overdue), summing amount and invoice count per bucket — the classic "aged debtors" report.
    /// </summary>
    Task<List<AgedDebtorBucket>> GetAgedDebtorsAsync(string tenantId);
}
