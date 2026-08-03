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

    /// <summary>Human-readable explanation of how RiskLevel was derived, in application order — mirrors ContactCreditScore.Reasons.</summary>
    public List<string> Reasons { get; set; } = [];

    /// <summary>This contact's share (0-100) of the total outstanding receivables across all at-risk contacts.</summary>
    public decimal ConcentrationPercent { get; set; }

    /// <summary>Populated only when the Xero contact has a CompanyNumber that matched a Companies House lookup.</summary>
    public string? CompanyNumber { get; set; }
    public string? CompaniesHouseStatus { get; set; }
    public bool CompaniesHouseDistressed { get; set; }
    public bool CompaniesHouseOverdueFilings { get; set; }
    public DateTime? CompanyIncorporationDate { get; set; }
    public bool CompaniesHouseHasInsolvencyHistory { get; set; }
    public bool CompaniesHouseHasCharges { get; set; }

    /// <summary>Broad sector from the company's primary SIC code, or null if no company number/SIC data on file.</summary>
    public string? CompaniesHouseSector { get; set; }

    /// <summary>Sectors UK research found have disproportionately higher late-payment rates — see CompanyProfile.IsHigherRiskSector.</summary>
    public bool CompaniesHouseHigherRiskSector { get; set; }
}
