using Microsoft.AspNetCore.Mvc;
using Xero.NetStandard.OAuth2.Client;
using XeroExtension.Web.Models;
using XeroExtension.Web.Services;

namespace XeroExtension.Web.Controllers;

[ApiController]
[Route("auth/xero")]
public class AuthController : ControllerBase
{
    private readonly IXeroClient _xeroClient;
    private readonly ITokenStore _tokenStore;

    // In a real app, derive userId from HttpContext.User claims.
    private const string UserId = "default-user";

    public AuthController(IXeroClient xeroClient, ITokenStore tokenStore)
    {
        _xeroClient = xeroClient;
        _tokenStore = tokenStore;
    }

    /// <summary>GET /auth/xero/connect — Redirects user to Xero's OAuth 2.0 login page.</summary>
    [HttpGet("connect")]
    public IActionResult Connect()
    {
        var url = _xeroClient.BuildLoginUri(state: Guid.NewGuid().ToString());
        return Redirect(url);
    }

    /// <summary>GET /auth/xero/callback — Xero redirects here after the user authorises the app.</summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest("Missing authorisation code.");

        var token = await _xeroClient.RequestAccessTokenAsync(code);

        var tenants = await _xeroClient.GetConnectionsAsync(token);
        var tenantIds = tenants.Select(t => t.TenantId.ToString()).ToArray();

        var tokenSet = new XeroTokenSet
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            ExpiresAt = new DateTimeOffset(token.ExpiresAtUtc, TimeSpan.Zero),
            Tenants = tenantIds
        };

        await _tokenStore.SaveAsync(UserId, tokenSet);

        return Ok(new { message = "Connected to Xero.", tenants = tenantIds });
    }

    /// <summary>DELETE /auth/xero/disconnect — Removes stored tokens.</summary>
    [HttpDelete("disconnect")]
    public async Task<IActionResult> Disconnect()
    {
        await _tokenStore.DeleteAsync(UserId);
        return Ok(new { message = "Disconnected from Xero." });
    }
}
