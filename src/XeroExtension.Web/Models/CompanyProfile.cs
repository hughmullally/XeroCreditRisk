namespace XeroExtension.Web.Models;

public class CompanyProfile
{
    private static readonly HashSet<string> DistressedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "dissolved", "liquidation", "receivership", "administration",
        "voluntary-arrangement", "insolvency-proceedings", "converted-closed"
    };

    /// <summary>Sectors where UK research (London Economics 2025, GOV.UK-commissioned; corroborated by FSB 2025) found
    /// disproportionately higher rates of late payment being a "big problem" — see docs/uk-sme-late-payment-cost-research.md.</summary>
    private static readonly HashSet<string> HigherRiskSectors = new(StringComparer.OrdinalIgnoreCase)
    {
        "Construction", "Professional, Scientific & Technical", "Administrative & Support Services"
    };

    public string CompanyNumber { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? IncorporationDate { get; set; }
    public bool AccountsOverdue { get; set; }
    public bool ConfirmationStatementOverdue { get; set; }
    public bool HasInsolvencyHistory { get; set; }
    public bool HasCharges { get; set; }
    public List<string> SicCodes { get; set; } = [];

    public bool IsDistressed => DistressedStatuses.Contains(Status);

    /// <summary>Broad UK SIC 2007 section derived from the primary (first-listed) SIC code, or null if none on file.</summary>
    public string? PrimarySector => SicCodes.Count > 0 ? SectorFromSicCode(SicCodes[0]) : null;

    public bool IsHigherRiskSector => PrimarySector is not null && HigherRiskSectors.Contains(PrimarySector);

    /// <summary>Maps a 5-digit UK SIC 2007 code to its broad section, using the standard division-number
    /// boundaries (e.g. 41–43 = Construction). Not a full description lookup — just enough to group and flag.</summary>
    private static string? SectorFromSicCode(string sicCode)
    {
        if (string.IsNullOrWhiteSpace(sicCode) || !int.TryParse(sicCode.AsSpan(0, Math.Min(2, sicCode.Length)), out var division))
            return null;

        return division switch
        {
            >= 1 and <= 3 => "Agriculture, Forestry & Fishing",
            >= 5 and <= 9 => "Mining & Quarrying",
            >= 10 and <= 33 => "Manufacturing",
            35 => "Energy Supply",
            >= 36 and <= 39 => "Water & Waste Management",
            >= 41 and <= 43 => "Construction",
            >= 45 and <= 47 => "Wholesale & Retail Trade",
            >= 49 and <= 53 => "Transportation & Storage",
            >= 55 and <= 56 => "Accommodation & Food Services",
            >= 58 and <= 63 => "Information & Communication",
            >= 64 and <= 66 => "Financial & Insurance",
            68 => "Real Estate",
            >= 69 and <= 75 => "Professional, Scientific & Technical",
            >= 77 and <= 82 => "Administrative & Support Services",
            84 => "Public Administration",
            85 => "Education",
            >= 86 and <= 88 => "Health & Social Work",
            >= 90 and <= 93 => "Arts & Entertainment",
            >= 94 and <= 96 => "Other Services",
            _ => null
        };
    }
}
