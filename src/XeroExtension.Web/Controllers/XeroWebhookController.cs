using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using XeroExtension.Web.Services;

namespace XeroExtension.Web.Controllers;

[ApiController]
[Route("webhooks/xero")]
public class XeroWebhookController : ControllerBase
{
    private readonly string _webhookKey;
    private readonly DashboardNotifier _notifier;
    private readonly ILogger<XeroWebhookController> _logger;

    public XeroWebhookController(IConfiguration configuration, DashboardNotifier notifier, ILogger<XeroWebhookController> logger)
    {
        _webhookKey = configuration["Xero:WebhookKey"] ?? string.Empty;
        _notifier = notifier;
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
        _notifier.NotifyChanged();
        return Ok();
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
