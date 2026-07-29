namespace XeroExtension.Web.Models;

public class ContactCreditScore
{
    public string ContactId { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;

    /// <summary>0-100, higher is safer.</summary>
    public int Score { get; set; }

    /// <summary>A-F, derived from Score.</summary>
    public string Grade { get; set; } = string.Empty;

    /// <summary>Line-by-line breakdown of how Score was derived from a baseline of 100, for drilldown display.</summary>
    public List<string> Reasons { get; set; } = [];
}
