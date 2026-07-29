using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using XeroExtension.Web.Services;

namespace XeroExtension.Web.Controllers;

[ApiController]
[Route("webhooks/xero")]
public class XeroWebhookController : ControllerBase
{
    private readonly string _webhookKey;
    private readonly DashboardNotifier _notifier;
    private readonly IXeroService _xeroService;
    private readonly ILogger<XeroWebhookController> _logger;

    public XeroWebhookController(
        IConfiguration configuration, DashboardNotifier notifier, IXeroService xeroService, ILogger<XeroWebhookController> logger)
    {
        _webhookKey = configuration["Xero:WebhookKey"] ?? string.Empty;
        _notifier = notifier;
        _xeroService = xeroService;
        _logger = logger;
    }

    /// <summary>
    /// POST /webhooks/xero — Xero calls this whenever a subscribed resource (invoices, contacts, etc.)
    /// changes in the real organisation. Every request's signature must be validated before trusting
    /// it — Xero's own "Intent to Receive" check specifically expects a 401 for a bad signature to
    /// confirm this endpoint actually verifies requests, not just accepts anything.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Receive()
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync();

        var signature = Request.Headers["x-xero-signature"].ToString();
        if (!SignatureIsValid(rawBody, signature))
        {
            _logger.LogWarning("Xero webhook signature validation failed.");
            return Unauthorized();
        }

        _logger.LogInformation("Xero webhook received and validated.");

        var changedContactIds = await ResolveChangedContactIdsAsync(rawBody);
        if (changedContactIds.Count > 0)
            _notifier.NotifyChanged(changedContactIds);

        return Ok();
    }

    /// <summary>Maps each event in the payload to the contact it affects — direct for CONTACT events, via a lookup for INVOICE events.</summary>
    private async Task<List<string>> ResolveChangedContactIdsAsync(string rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
            return [];

        WebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<WebhookPayload>(rawBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Xero webhook payload.");
            return [];
        }

        if (payload?.Events is not { Count: > 0 })
            return [];

        var contactIds = new HashSet<string>();

        foreach (var evt in payload.Events)
        {
            if (!Guid.TryParse(evt.ResourceId, out var resourceId))
                continue;

            if (string.Equals(evt.EventCategory, "CONTACT", StringComparison.OrdinalIgnoreCase))
            {
                contactIds.Add(resourceId.ToString());
            }
            else if (string.Equals(evt.EventCategory, "INVOICE", StringComparison.OrdinalIgnoreCase))
            {
                var contactId = await _xeroService.GetInvoiceContactIdAsync(evt.TenantId, resourceId);
                if (contactId.HasValue)
                    contactIds.Add(contactId.Value.ToString());
            }
        }

        return contactIds.ToList();
    }

    private class WebhookPayload
    {
        [JsonPropertyName("events")] public List<WebhookEvent> Events { get; set; } = [];
    }

    private class WebhookEvent
    {
        [JsonPropertyName("resourceId")] public string ResourceId { get; set; } = "";
        [JsonPropertyName("eventCategory")] public string EventCategory { get; set; } = "";
        [JsonPropertyName("eventType")] public string EventType { get; set; } = "";
        [JsonPropertyName("tenantId")] public string TenantId { get; set; } = "";
    }

    private string ComputeSignature(string rawBody)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_webhookKey));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        return Convert.ToBase64String(computedHash);
    }

    private bool SignatureIsValid(string rawBody, string signatureHeader)
    {
        if (string.IsNullOrEmpty(_webhookKey) || string.IsNullOrEmpty(signatureHeader))
            return false;

        var computedSignature = ComputeSignature(rawBody);

        var computedBytes = Encoding.UTF8.GetBytes(computedSignature);
        var providedBytes = Encoding.UTF8.GetBytes(signatureHeader);
        return computedBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(computedBytes, providedBytes);
    }
}
