namespace XeroExtension.Web.Models;

public class CompanyProfile
{
    private static readonly HashSet<string> DistressedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "dissolved", "liquidation", "receivership", "administration",
        "voluntary-arrangement", "insolvency-proceedings", "converted-closed"
    };

    public string CompanyNumber { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? IncorporationDate { get; set; }
    public bool AccountsOverdue { get; set; }
    public bool ConfirmationStatementOverdue { get; set; }
    public bool HasInsolvencyHistory { get; set; }
    public bool HasCharges { get; set; }

    public bool IsDistressed => DistressedStatuses.Contains(Status);
}
