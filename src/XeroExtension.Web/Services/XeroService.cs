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

    public async Task<List<Contact>> GetContactsWithoutSalesInvoicesAsync(string tenantId)
    {
        var contacts = await GetContactsAsync(tenantId);
        var invoices = await GetInvoicesAsync(tenantId);

        var contactIdsWithInvoices = invoices
            .Where(i => i.Type == Invoice.TypeEnum.ACCREC)
            .Select(i => i.Contact.ContactID)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();

        return contacts
            .Where(c => c.ContactID.HasValue && !contactIdsWithInvoices.Contains(c.ContactID.Value))
            .ToList();
    }

    public async Task<Dictionary<Guid, int>> GetSalesInvoiceCountsByContactAsync(string tenantId)
    {
        var invoices = await GetInvoicesAsync(tenantId);

        return invoices
            .Where(i => i.Type == Invoice.TypeEnum.ACCREC && i.Contact.ContactID.HasValue)
            .GroupBy(i => i.Contact.ContactID!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<Guid> CreateSalesInvoiceAsync(string tenantId, Guid contactId, DateTime dueDate, decimal amount, string description)
    {
        var token = await GetValidTokenAsync();

        var invoice = new Invoice
        {
            Type = Invoice.TypeEnum.ACCREC,
            Contact = new Contact { ContactID = contactId },
            Date = dueDate.AddDays(-14),
            DueDate = dueDate,
            Status = Invoice.StatusEnum.AUTHORISED,
            LineItems =
            [
                new LineItem
                {
                    Description = description,
                    Quantity = 1,
                    UnitAmount = amount,
                    AccountCode = "200"
                }
            ]
        };

        var result = await _accountingApi.CreateInvoicesAsync(token.AccessToken, tenantId, new Invoices { _Invoices = [invoice] });
        return result._Invoices![0].InvoiceID!.Value;
    }

    public async Task<Guid?> GetInvoiceContactIdAsync(string tenantId, Guid invoiceId)
    {
        var token = await GetValidTokenAsync();
        var response = await _accountingApi.GetInvoicesAsync(token.AccessToken, tenantId, iDs: [invoiceId]);
        return response?._Invoices?.FirstOrDefault()?.Contact.ContactID;
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
