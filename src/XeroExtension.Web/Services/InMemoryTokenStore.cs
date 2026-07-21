using Microsoft.Extensions.Caching.Memory;
using XeroExtension.Web.Models;

namespace XeroExtension.Web.Services;

/// <summary>
/// In-memory token store. Replace with a persistent store (DB, Redis, etc.) for production.
/// </summary>
public class InMemoryTokenStore : ITokenStore
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan TokenExpiry = TimeSpan.FromHours(24);

    public InMemoryTokenStore(IMemoryCache cache) => _cache = cache;

    public Task SaveAsync(string userId, XeroTokenSet tokenSet)
    {
        _cache.Set(CacheKey(userId), tokenSet, TokenExpiry);
        return Task.CompletedTask;
    }

    public Task<XeroTokenSet?> GetAsync(string userId)
    {
        _cache.TryGetValue(CacheKey(userId), out XeroTokenSet? tokenSet);
        return Task.FromResult(tokenSet);
    }

    public Task DeleteAsync(string userId)
    {
        _cache.Remove(CacheKey(userId));
        return Task.CompletedTask;
    }

    private static string CacheKey(string userId) => $"xero_token:{userId}";
}
