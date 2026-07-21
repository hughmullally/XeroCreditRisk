using XeroExtension.Web.Models;

namespace XeroExtension.Web.Services;

public interface ITokenStore
{
    Task SaveAsync(string userId, XeroTokenSet tokenSet);
    Task<XeroTokenSet?> GetAsync(string userId);
    Task DeleteAsync(string userId);
}
