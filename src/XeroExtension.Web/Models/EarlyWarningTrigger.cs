namespace XeroExtension.Web.Models;

public enum EarlyWarningType
{
    /// <summary>A contact with a clean on-time payment history was late for the first time.</summary>
    FirstLatePayment,

    /// <summary>Payment lateness is trending meaningfully worse, even if the current risk tier hasn't caught up yet.</summary>
    AcceleratingLateness,

    /// <summary>The contact currently owes more than their recommended credit limit.</summary>
    ExceedsRecommendedLimit,

    /// <summary>Companies House shows the contact's company as dissolved/in administration/liquidation etc., or with overdue statutory filings.</summary>
    CompanyDistressSignal,

    /// <summary>Companies House shows the company has been through insolvency proceedings before, even though it's currently trading — worth a human glance, not an automatic risk escalation.</summary>
    PriorInsolvencyHistory,

    /// <summary>A single contact accounts for an outsized share of total outstanding receivables — risky even if that contact is individually paying fine.</summary>
    ConcentrationRisk
}

public class EarlyWarningTrigger
{
    public string ContactId { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public EarlyWarningType Type { get; set; }
    public string Message { get; set; } = string.Empty;
}
