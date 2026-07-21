using Xero.NetStandard.OAuth2.Api;
using Xero.NetStandard.OAuth2.Client;
using Xero.NetStandard.OAuth2.Model.Accounting;
using Xero.NetStandard.OAuth2.Token;
using XeroExtension.Web.Models;

namespace XeroExtension.Web.Services;

public class XeroService : IXeroService
{
    private readonly AccountingApi _accountingApi = new();
    private readonly ITokenStore _tokenStore;
    private readonly IXeroClient _xeroClient;
    private readonly ILogger<XeroService> _logger;

    // In a real app, userId comes from the current authenticated user's claims.
    // This is a placeholder — wire it to your auth context.
    private const string UserId = "default-user";

    public XeroService(
        ITokenStore tokenStore,
        IXeroClient xeroClient,
        ILogger<XeroService> logger)
    {
        _tokenStore = tokenStore;
        _xeroClient = xeroClient;
        _logger = logger;
    }

    public async Task<List<Invoice>> GetInvoicesAsync(string tenantId)
    {
        var token = await GetValidTokenAsync();
        var response = await _accountingApi.GetInvoicesAsync(token.AccessToken, tenantId);
        return response?._Invoices ?? [];
    }

    public async Task<List<Contact>> GetContactsAsync(string tenantId)
    {
        var token = await GetValidTokenAsync();
        var response = await _accountingApi.GetContactsAsync(token.AccessToken, tenantId);
        return response?._Contacts ?? [];
    }

    public async Task<List<Account>> GetAccountsAsync(string tenantId)
    {
        var token = await GetValidTokenAsync();
        var response = await _accountingApi.GetAccountsAsync(token.AccessToken, tenantId);
        return response?._Accounts ?? [];
    }

    public async Task<Guid> EnsureContactGroupAsync(string tenantId, string groupName)
    {
        var token = await GetValidTokenAsync();

        var existing = await _accountingApi.GetContactGroupsAsync(token.AccessToken, tenantId);
        var existingGroup = existing?._ContactGroups?.FirstOrDefault(g => g.Name == groupName);
        if (existingGroup?.ContactGroupID is { } existingId)
            return existingId;

        var created = await _accountingApi.CreateContactGroupAsync(token.AccessToken, tenantId,
            new ContactGroups { _ContactGroups = [new ContactGroup { Name = groupName }] });
        return created._ContactGroups![0].ContactGroupID!.Value;
    }

    public async Task ReplaceContactGroupMembersAsync(string tenantId, Guid contactGroupId, IReadOnlyCollection<string> contactIds)
    {
        var token = await GetValidTokenAsync();

        await _accountingApi.DeleteContactGroupContactsAsync(token.AccessToken, tenantId, contactGroupId);

        if (contactIds.Count == 0)
            return;

        var contacts = new Contacts
        {
            _Contacts = contactIds.Select(id => new Contact { ContactID = Guid.Parse(id) }).ToList()
        };
        await _accountingApi.CreateContactGroupContactsAsync(token.AccessToken, tenantId, contactGroupId, contacts);
    }

    private async Task<XeroTokenSet> GetValidTokenAsync()
    {
        var tokenSet = await _tokenStore.GetAsync(UserId)
            ?? throw new NotConnectedException("No Xero token found. User must connect to Xero first.");

        if (!tokenSet.IsExpired)
            return tokenSet;

        _logger.LogInformation("Xero access token expired, refreshing...");
        var refreshed = await _xeroClient.RefreshAccessTokenAsync(
            new XeroOAuth2Token { RefreshToken = tokenSet.RefreshToken });

        var updated = new XeroTokenSet
        {
            AccessToken = refreshed.AccessToken,
            RefreshToken = refreshed.RefreshToken,
            ExpiresAt = new DateTimeOffset(refreshed.ExpiresAtUtc, TimeSpan.Zero),
            Tenants = tokenSet.Tenants
        };

        await _tokenStore.SaveAsync(UserId, updated);
        return updated;
    }
}
