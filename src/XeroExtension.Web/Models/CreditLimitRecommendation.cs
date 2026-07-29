namespace XeroExtension.Web.Models;

public class CreditLimitRecommendation
{
    public string ContactId { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public decimal AverageInvoiceAmount { get; set; }
    public decimal CurrentOutstanding { get; set; }
    public CreditRiskLevel RiskLevel { get; set; }
    public decimal RecommendedCreditLimit { get; set; }
    public string Rationale { get; set; } = string.Empty;

    /// <summary>Line-by-line breakdown of how RecommendedCreditLimit was derived, for drilldown display.</summary>
    public List<string> Reasons { get; set; } = [];

    /// <summary>True when the contact currently owes more than the recommended limit — a signal to review before extending further credit.</summary>
    public bool ExceedsRecommendedLimit => CurrentOutstanding > RecommendedCreditLimit;
}
