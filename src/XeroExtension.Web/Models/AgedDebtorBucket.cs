namespace XeroExtension.Web.Models;

/// <summary>One age band of the classic "aged debtors" report — e.g. "31-60 days".</summary>
public class AgedDebtorBucket
{
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int InvoiceCount { get; set; }
}
