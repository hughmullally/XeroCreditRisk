using Microsoft.AspNetCore.Mvc;
using XeroExtension.Web.Services;

namespace XeroExtension.Web.Controllers;

/// <summary>Demo/testing utilities only — not part of the real credit risk feature set.</summary>
[Route("dev")]
public class DevController : ControllerBase
{
    private readonly IXeroService _xeroService;

    // Contacts that aren't realistic customers to invoice (the org's own name, tax authority, the org owner).
    private static readonly HashSet<string> ExcludedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "HMRC", "Xero", "Hugh Mullally"
    };

    // Cycled across candidates to spread them across risk tiers (High/Medium/Low/Current/not-yet-due).
    private static readonly int[] DueDateOffsetDays = [-75, -45, -20, -5, 10, 25];

    public DevController(IXeroService xeroService) => _xeroService = xeroService;

    /// <summary>
    /// POST /dev/seed-invoices?tenantId={id}&amp;count={n} — creates test sales invoices for contacts
    /// that don't have any yet, so more of the Demo Company shows up in the credit risk views.
    /// </summary>
    [HttpPost("seed-invoices")]
    public async Task<IActionResult> SeedInvoices([FromQuery] string tenantId, [FromQuery] int count = 8)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return BadRequest("tenantId is required.");

        var candidates = (await _xeroService.GetContactsWithoutSalesInvoicesAsync(tenantId))
            .Where(c => !ExcludedNames.Contains(c.Name))
            .Take(count)
            .ToList();

        var now = DateTime.UtcNow;
        var created = new List<object>();

        for (var i = 0; i < candidates.Count; i++)
        {
            var contact = candidates[i];
            var dueDate = now.AddDays(DueDateOffsetDays[i % DueDateOffsetDays.Length]);
            var amount = 150 + i * 137 % 900;

            var invoiceId = await _xeroService.CreateSalesInvoiceAsync(
                tenantId, contact.ContactID!.Value, dueDate, amount, $"Test invoice for {contact.Name}");

            created.Add(new { contactId = contact.ContactID, contact.Name, dueDate, amount, invoiceId });
        }

        return Ok(new { message = $"Created {created.Count} test invoices.", invoices = created });
    }
}
