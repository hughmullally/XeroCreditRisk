using Microsoft.AspNetCore.Mvc;
using XeroExtension.Web.Services;

namespace XeroExtension.Web.Controllers;

[ApiController]
[Route("api/xero")]
public class XeroController : ControllerBase
{
    private readonly IXeroService _xeroService;
    private readonly ICreditRiskService _creditRiskService;

    public XeroController(IXeroService xeroService, ICreditRiskService creditRiskService)
    {
        _xeroService = xeroService;
        _creditRiskService = creditRiskService;
    }

    /// <summary>GET /api/xero/invoices?tenantId={id}</summary>
    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices([FromQuery] string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return BadRequest("tenantId is required.");

        var invoices = await _xeroService.GetInvoicesAsync(tenantId);
        return Ok(invoices);
    }

    /// <summary>GET /api/xero/contacts?tenantId={id}</summary>
    [HttpGet("contacts")]
    public async Task<IActionResult> GetContacts([FromQuery] string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return BadRequest("tenantId is required.");

        var contacts = await _xeroService.GetContactsAsync(tenantId);
        return Ok(contacts);
    }

    /// <summary>GET /api/xero/accounts?tenantId={id}</summary>
    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts([FromQuery] string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return BadRequest("tenantId is required.");

        var accounts = await _xeroService.GetAccountsAsync(tenantId);
        return Ok(accounts);
    }

    /// <summary>GET /api/xero/credit-risk?tenantId={id} — customers ranked by overdue-invoice risk.</summary>
    [HttpGet("credit-risk")]
    public async Task<IActionResult> GetCreditRisk([FromQuery] string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return BadRequest("tenantId is required.");

        var risk = await _creditRiskService.GetContactRiskAsync(tenantId);
        return Ok(risk);
    }

    /// <summary>POST /api/xero/credit-risk/sync?tenantId={id} — writes risk levels back to Xero as Contact Groups.</summary>
    [HttpPost("credit-risk/sync")]
    public async Task<IActionResult> SyncCreditRisk([FromQuery] string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return BadRequest("tenantId is required.");

        await _creditRiskService.SyncRiskGroupsToXeroAsync(tenantId);
        return Ok(new { message = "Risk groups synced to Xero." });
    }
}
