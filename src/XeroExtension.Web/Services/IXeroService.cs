using Xero.NetStandard.OAuth2.Model.Accounting;

namespace XeroExtension.Web.Services;

public interface IXeroService
{
    Task<List<Invoice>> GetInvoicesAsync(string tenantId);
    Task<List<Contact>> GetContactsAsync(string tenantId);
    Task<List<Account>> GetAccountsAsync(string tenantId);
}
