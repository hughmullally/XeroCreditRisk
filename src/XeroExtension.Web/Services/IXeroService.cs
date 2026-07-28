using Xero.NetStandard.OAuth2.Model.Accounting;

namespace XeroExtension.Web.Services;

public interface IXeroService
{
    Task<List<Invoice>> GetInvoicesAsync(string tenantId);
    Task<List<Contact>> GetContactsAsync(string tenantId);
    Task<List<Account>> GetAccountsAsync(string tenantId);

    /// <summary>Finds a Xero Contact Group by name, creating it if it doesn't exist yet, and returns its ID.</summary>
    Task<Guid> EnsureContactGroupAsync(string tenantId, string groupName);

    /// <summary>Replaces a Contact Group's membership wholesale: clears existing members, then adds the given contacts.</summary>
    Task ReplaceContactGroupMembersAsync(string tenantId, Guid contactGroupId, IReadOnlyCollection<string> contactIds);

    /// <summary>Contacts that have no ACCREC (sales) invoices at all yet.</summary>
    Task<List<Contact>> GetContactsWithoutSalesInvoicesAsync(string tenantId);

    /// <summary>Creates and authorises a single sales invoice. Test/demo data seeding only.</summary>
    Task<Guid> CreateSalesInvoiceAsync(string tenantId, Guid contactId, DateTime dueDate, decimal amount, string description);
}
