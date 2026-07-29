using System.Net;
using Microsoft.AspNetCore.Mvc;
using XeroExtension.Web.Services;

namespace XeroExtension.Web.Controllers;

/// <summary>Demo/testing utilities only — not part of the real credit risk feature set.</summary>
[Route("dev")]
public class DevController : ControllerBase
{
    private const string DefaultTenantId = "e5be2af3-6963-44b0-9f7e-855921499c72";

    private readonly IXeroService _xeroService;

    // Contacts that aren't realistic customers to invoice (the org's own name, tax authority, the org owner).
    private static readonly HashSet<string> ExcludedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "HMRC", "Xero", "Hugh Mullally"
    };

    // Cycled across invoices created in a batch to spread them across risk tiers (High/Medium/Low/Current/not-yet-due).
    private static readonly int[] DueDateOffsetDays = [-75, -45, -20, -5, 10, 25];

    // Contacts with a deliberately-chosen, hand-set company number that bulk population must never
    // overwrite. Only City Limousines (dissolved) and Ridgeway University (liquidation) are kept as
    // problem-scenario demo cases; the rest are clean.
    private static readonly HashSet<string> PreservedCompanyNumberContacts = new(StringComparer.OrdinalIgnoreCase)
    {
        "City Limousines", "Ridgeway University", "Port & Philip Freight",
        "Basket Shop", "Marine Systems", "Bayside Club", "Boom FM"
    };

    // Genuinely clean, active companies (verified: not overdue on accounts/confirmation statement, no
    // insolvency history) — the majority of the population pool, so most seeded contacts show no warnings.
    private static readonly string[] CleanCompanyNumberPool =
    [
        "17251784", // Surrey Hills Holdings Limited — very young (2026)
        "00445790", // Tesco PLC — old, has registered charges (not a warning on its own)
        "NI625848", // 3 Salmon Restaurants Limited
        "SC006657", // Sunarian Holdings Limited
        "SC031305", // John Croal (Electrical Contractors) Limited
        "SC038468", // Reema Limited
        "07409441", // Add-V Limited
        "07431006", // Action Plus Staffing Agency Limited
        "07795943", // Account Management Expert Limited
        "06753023"  // Accident Care UK Limited
    ];

    public DevController(IXeroService xeroService) => _xeroService = xeroService;

    /// <summary>GET /dev?tenantId={id} — a grid of every counterparty with an editable "invoices to seed" count each.</summary>
    [HttpGet]
    public async Task<ContentResult> Index([FromQuery] string? tenantId)
    {
        var effectiveTenantId = string.IsNullOrWhiteSpace(tenantId) ? DefaultTenantId : tenantId;

        var contacts = (await _xeroService.GetContactsAsync(effectiveTenantId))
            .Where(c => c.ContactID.HasValue && !ExcludedNames.Contains(c.Name))
            .OrderBy(c => c.Name)
            .ToList();

        var invoiceCounts = await _xeroService.GetSalesInvoiceCountsByContactAsync(effectiveTenantId);

        var rows = string.Join("\n", contacts.Select(c =>
        {
            var count = invoiceCounts.GetValueOrDefault(c.ContactID!.Value, 0);
            var companyNumberDisplay = string.IsNullOrWhiteSpace(c.CompanyNumber) ? "—" : WebUtility.HtmlEncode(c.CompanyNumber);
            return $"""
                <div class="contact-row">
                  <label class="contact-label" title="{WebUtility.HtmlEncode(c.Name)}">{WebUtility.HtmlEncode(c.Name)} <span class="current-count">({count})</span></label>
                  <span class="company-number">{companyNumberDisplay}</span>
                  <input type="number" class="seed-count" data-contact-id="{c.ContactID}" value="0" min="0" max="50" />
                </div>
                """;
        }));

        var html = $$"""
            <!DOCTYPE html>
            <html>
            <head>
              <meta charset="utf-8" />
              <title>Dev Tools</title>
              <style>
                body { font-family: -apple-system, "Segoe UI", sans-serif; margin: 2rem; background: #f7f7f8; color: #222; }
                h1 { font-size: 1.4rem; }
                .tenant-form { margin-bottom: 1rem; font-size: 0.85rem; }
                .tenant-form input { padding: 0.4rem; border: 1px solid #ddd; border-radius: 4px; width: 320px; }
                .tenant-form button { padding: 0.4rem 0.8rem; border: none; border-radius: 4px; background: #eee; cursor: pointer; }
                .contact-grid { column-count: 3; column-gap: 1.5rem; background: white; box-shadow: 0 1px 3px rgba(0,0,0,0.1); border-radius: 4px; padding: 0.5rem 1rem; }
                .contact-row { display: flex; align-items: center; justify-content: space-between; gap: 0.5rem; padding: 0.15rem 0; border-bottom: 1px solid #eee; break-inside: avoid; }
                .contact-label { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 0.8rem; }
                .current-count { color: #999; }
                .company-number { flex-shrink: 0; width: 70px; font-family: monospace; font-size: 0.75rem; color: #666; text-align: right; }
                input.seed-count { width: 48px; padding: 0.15rem; border: 1px solid #ddd; border-radius: 4px; font-size: 0.85rem; flex-shrink: 0; }
                .dev-btn { margin-top: 1.2rem; margin-right: 0.6rem; padding: 0.6rem 1.2rem; background: #13b5ea; color: white; border: none; border-radius: 4px; font-size: 1rem; cursor: pointer; }
                .dev-btn:disabled { background: #aaa; cursor: default; }
                .dev-btn:hover:not(:disabled) { background: #0f9fcf; }
                .dev-btn.secondary { background: #6c757d; }
                .dev-btn.secondary:hover:not(:disabled) { background: #5a6268; }
                #result { margin-top: 1.5rem; padding: 1rem; background: white; border-radius: 4px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); white-space: pre-wrap; font-family: monospace; font-size: 0.85rem; display: none; max-width: 700px; max-height: 400px; overflow: auto; }
              </style>
            </head>
            <body>
              <h1>Seed Test Invoices</h1>
              <form class="tenant-form" method="get" action="/dev">
                <label for="tenantId">Tenant ID</label>
                <input type="text" id="tenantId" name="tenantId" value="{{effectiveTenantId}}" />
                <button type="submit">Load</button>
              </form>

              <div class="contact-grid">
                {{rows}}
              </div>

              <button id="seedAllBtn" class="dev-btn">Seed All</button>
              <button id="populateChBtn" class="dev-btn secondary">Populate Companies House Numbers</button>
              <div id="result"></div>

              <script>
                // Show the result of the seeding that just triggered this reload (the reload itself
                // is what refreshes the "Current Invoices" counts, since they're rendered server-side).
                (() => {
                  const pending = sessionStorage.getItem('seedResult');
                  if (!pending) return;
                  sessionStorage.removeItem('seedResult');
                  const result = document.getElementById('result');
                  result.style.display = 'block';
                  result.textContent = pending;
                })();

                document.getElementById('seedAllBtn').addEventListener('click', async () => {
                  const inputs = Array.from(document.querySelectorAll('.seed-count'));
                  const items = inputs
                    .map(input => ({ contactId: input.dataset.contactId, count: parseInt(input.value, 10) || 0 }))
                    .filter(item => item.count > 0);

                  if (items.length === 0) {
                    alert('Enter a count greater than 0 for at least one counterparty.');
                    return;
                  }

                  const btn = document.getElementById('seedAllBtn');
                  const result = document.getElementById('result');
                  btn.disabled = true;
                  btn.textContent = 'Seeding...';
                  result.style.display = 'block';
                  result.textContent = 'Working...';

                  try {
                    const res = await fetch(`/dev/seed-invoices-bulk?tenantId={{effectiveTenantId}}`, {
                      method: 'POST',
                      headers: { 'Content-Type': 'application/json' },
                      body: JSON.stringify(items)
                    });
                    const data = await res.json();
                    sessionStorage.setItem('seedResult', JSON.stringify(data, null, 2));
                    location.reload();
                  } catch (err) {
                    result.textContent = 'Error: ' + err;
                    btn.disabled = false;
                    btn.textContent = 'Seed All';
                  }
                });

                document.getElementById('populateChBtn').addEventListener('click', async () => {
                  const btn = document.getElementById('populateChBtn');
                  const result = document.getElementById('result');
                  btn.disabled = true;
                  btn.textContent = 'Populating...';
                  result.style.display = 'block';
                  result.textContent = 'Working...';

                  try {
                    const res = await fetch(`/dev/populate-company-numbers?tenantId={{effectiveTenantId}}`, { method: 'POST' });
                    const data = await res.json();
                    result.textContent = JSON.stringify(data, null, 2);
                  } catch (err) {
                    result.textContent = 'Error: ' + err;
                  } finally {
                    btn.disabled = false;
                    btn.textContent = 'Populate Companies House Numbers';
                  }
                });
              </script>
            </body>
            </html>
            """;

        return Content(html, "text/html");
    }

    public record SeedInvoiceRequestItem(string ContactId, int Count);

    /// <summary>
    /// POST /dev/seed-invoices-bulk?tenantId={id} — creates the requested number of test sales
    /// invoices per contact, spreading due dates across a fixed offset pattern for variety.
    /// </summary>
    [HttpPost("seed-invoices-bulk")]
    public async Task<IActionResult> SeedInvoicesBulk([FromQuery] string tenantId, [FromBody] List<SeedInvoiceRequestItem> items)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return BadRequest("tenantId is required.");

        var contacts = (await _xeroService.GetContactsAsync(tenantId))
            .Where(c => c.ContactID.HasValue)
            .ToDictionary(c => c.ContactID!.Value.ToString()!, c => c);

        var now = DateTime.UtcNow;
        var created = new List<object>();
        var index = 0;

        foreach (var item in items.Where(i => i.Count > 0))
        {
            if (!contacts.TryGetValue(item.ContactId, out var contact))
                continue;

            for (var i = 0; i < item.Count; i++)
            {
                var dueDate = now.AddDays(DueDateOffsetDays[index % DueDateOffsetDays.Length]);
                var amount = 150 + index * 137 % 900;

                var invoiceId = await _xeroService.CreateSalesInvoiceAsync(
                    tenantId, contact.ContactID!.Value, dueDate, amount, $"Test invoice for {contact.Name}");

                created.Add(new { contactId = item.ContactId, contact.Name, dueDate, amount, invoiceId });
                index++;
            }
        }

        return Ok(new { message = $"Created {created.Count} test invoices.", invoices = created });
    }

    /// <summary>
    /// POST /dev/populate-company-numbers?tenantId={id} — (re)sets a Companies House number on every
    /// contact except the ones in PreservedCompanyNumberContacts, mostly cycling through verified clean
    /// active companies (so most contacts show no Companies House warnings), with a minority of problem
    /// scenarios (dissolved/liquidation/administration/etc.) mixed in for demo variety.
    /// </summary>
    [HttpPost("populate-company-numbers")]
    public async Task<IActionResult> PopulateCompanyNumbers([FromQuery] string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return BadRequest("tenantId is required.");

        var candidates = (await _xeroService.GetContactsAsync(tenantId))
            .Where(c => c.ContactID.HasValue && !ExcludedNames.Contains(c.Name) && !PreservedCompanyNumberContacts.Contains(c.Name))
            .ToList();

        var updated = new List<object>();

        for (var i = 0; i < candidates.Count; i++)
        {
            var contact = candidates[i];

            // Every newly-populated contact gets a genuinely clean, active company — the two
            // preserved contacts already cover the problem-scenario demo cases.
            var companyNumber = CleanCompanyNumberPool[i % CleanCompanyNumberPool.Length];

            await _xeroService.SetContactCompanyNumberAsync(tenantId, contact.ContactID!.Value, companyNumber);

            updated.Add(new { contactId = contact.ContactID, contact.Name, companyNumber });
        }

        return Ok(new { message = $"Set a company number on {updated.Count} contacts.", updated });
    }

    /// <summary>
    /// POST /dev/set-company-number?tenantId={id}&amp;contactId={id}&amp;companyNumber={number} — sets a
    /// single contact's Companies House number directly, bypassing the bulk population pool. Useful for
    /// one-off manual corrections without touching everything else.
    /// </summary>
    [HttpPost("set-company-number")]
    public async Task<IActionResult> SetCompanyNumber([FromQuery] string tenantId, [FromQuery] string contactId, [FromQuery] string companyNumber)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || !Guid.TryParse(contactId, out var id) || string.IsNullOrWhiteSpace(companyNumber))
            return BadRequest("tenantId, contactId, and companyNumber are all required, and contactId must be a valid GUID.");

        await _xeroService.SetContactCompanyNumberAsync(tenantId, id, companyNumber);
        return Ok(new { message = $"Set company number {companyNumber} on contact {contactId}." });
    }
}
