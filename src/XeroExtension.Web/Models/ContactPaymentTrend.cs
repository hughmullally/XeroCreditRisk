namespace XeroExtension.Web.Models;

public class PaymentHistoryEntry
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public DateTime PaidDate { get; set; }

    /// <summary>Days between due date and paid date. Negative means paid early.</summary>
    public int DaysLate { get; set; }
}

public class ContactPaymentTrend
{
    public string ContactId { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;

    /// <summary>Chronological (oldest first) history of this contact's paid invoices.</summary>
    public List<PaymentHistoryEntry> History { get; set; } = [];

    public double AverageDaysLate { get; set; }

    /// <summary>
    /// Recent-half average minus earlier-half average. Positive means payments are trending
    /// later (worsening); negative means trending earlier (improving). 0 if too little history
    /// (fewer than 2 paid invoices) to compare.
    /// </summary>
    public double TrendDelta { get; set; }
}
