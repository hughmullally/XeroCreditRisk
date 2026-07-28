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

    /// <summary>GET /dev — a small form for triggering test-invoice seeding without needing curl.</summary>
    [HttpGet]
    public ContentResult Index()
    {
        const string html = """
            <!DOCTYPE html>
            <html>
            <head>
              <meta charset="utf-8" />
              <title>Dev Tools</title>
              <style>
                body { font-family: -apple-system, "Segoe UI", sans-serif; margin: 2rem; background: #f7f7f8; color: #222; max-width: 500px; }
                h1 { font-size: 1.4rem; }
                label { display: block; margin-top: 1rem; font-size: 0.85rem; font-weight: 600; color: #666; }
                input { width: 100%; box-sizing: border-box; padding: 0.5rem; margin-top: 0.3rem; border: 1px solid #ddd; border-radius: 4px; font-size: 1rem; }
                button { margin-top: 1.2rem; padding: 0.6rem 1.2rem; background: #13b5ea; color: white; border: none; border-radius: 4px; font-size: 1rem; cursor: pointer; }
                button:disabled { background: #aaa; cursor: default; }
                button:hover:not(:disabled) { background: #0f9fcf; }
                #result { margin-top: 1.5rem; padding: 1rem; background: white; border-radius: 4px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); white-space: pre-wrap; font-family: monospace; font-size: 0.85rem; display: none; }
              </style>
            </head>
            <body>
              <h1>Seed Test Invoices</h1>
              <p>Creates real sales invoices in Xero for contacts that don't have any yet.</p>
              <form id="seedForm">
                <label for="tenantId">Tenant ID</label>
                <input type="text" id="tenantId" value="e5be2af3-6963-44b0-9f7e-855921499c72" required />

                <label for="count">Number of contacts to seed</label>
                <input type="number" id="count" value="8" min="1" max="30" />

                <button type="submit" id="submitBtn">Create invoices</button>
              </form>
              <div id="result"></div>

              <script>
                document.getElementById('seedForm').addEventListener('submit', async (e) => {
                  e.preventDefault();
                  const btn = document.getElementById('submitBtn');
                  const result = document.getElementById('result');
                  const tenantId = document.getElementById('tenantId').value;
                  const count = document.getElementById('count').value;

                  btn.disabled = true;
                  btn.textContent = 'Creating...';
                  result.style.display = 'block';
                  result.textContent = 'Working...';

                  try {
                    const res = await fetch(`/dev/seed-invoices?tenantId=${encodeURIComponent(tenantId)}&count=${encodeURIComponent(count)}`, { method: 'POST' });
                    const data = await res.json();
                    result.textContent = JSON.stringify(data, null, 2);
                  } catch (err) {
                    result.textContent = 'Error: ' + err;
                  } finally {
                    btn.disabled = false;
                    btn.textContent = 'Create invoices';
                  }
                });
              </script>
            </body>
            </html>
            """;

        return Content(html, "text/html");
    }

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
