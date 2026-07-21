namespace XeroExtension.Web.Models;

public enum CreditRiskLevel { Low, Medium, High }

public class ContactCreditRisk
{
    public string ContactId { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public int OverdueInvoiceCount { get; set; }
    public decimal OverdueAmount { get; set; }
    public int OldestOverdueDays { get; set; }
    public CreditRiskLevel RiskLevel { get; set; }
}
