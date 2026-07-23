namespace XeroExtension.Web.Models;

public enum EarlyWarningType
{
    /// <summary>A contact with a clean on-time payment history was late for the first time.</summary>
    FirstLatePayment,

    /// <summary>Payment lateness is trending meaningfully worse, even if the current risk tier hasn't caught up yet.</summary>
    AcceleratingLateness,

    /// <summary>The contact currently owes more than their recommended credit limit.</summary>
    ExceedsRecommendedLimit
}

public class EarlyWarningTrigger
{
    public string ContactId { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public EarlyWarningType Type { get; set; }
    public string Message { get; set; } = string.Empty;
}
