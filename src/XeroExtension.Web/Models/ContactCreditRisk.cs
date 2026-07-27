namespace XeroExtension.Web.Models;

public enum CreditRiskLevel { Current, Low, Medium, High }

public class ContactCreditRisk
{
    public string ContactId { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public int OutstandingInvoiceCount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public int OverdueInvoiceCount { get; set; }
    public decimal OverdueAmount { get; set; }
    public int OldestOverdueDays { get; set; }
    public CreditRiskLevel RiskLevel { get; set; }

    /// <summary>Populated only when the Xero contact has a CompanyNumber that matched a Companies House lookup.</summary>
    public string? CompanyNumber { get; set; }
    public string? CompaniesHouseStatus { get; set; }
    public bool CompaniesHouseDistressed { get; set; }
    public bool CompaniesHouseOverdueFilings { get; set; }
}
