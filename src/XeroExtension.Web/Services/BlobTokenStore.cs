using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using XeroExtension.Web.Models;

namespace XeroExtension.Web.Services;

/// <summary>Blob-backed token store — survives app restarts/redeploys, unlike InMemoryTokenStore.</summary>
public class BlobTokenStore : ITokenStore
{
    private readonly BlobContainerClient _container;

    public BlobTokenStore(BlobContainerClient container) => _container = container;

    public async Task SaveAsync(string userId, XeroTokenSet tokenSet)
    {
        var blob = _container.GetBlobClient(BlobName(userId));
        var json = JsonSerializer.Serialize(tokenSet);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await blob.UploadAsync(stream, overwrite: true);
    }

    public async Task<XeroTokenSet?> GetAsync(string userId)
    {
        var blob = _container.GetBlobClient(BlobName(userId));
        if (!await blob.ExistsAsync())
            return null;

        var response = await blob.DownloadContentAsync();
        return response.Value.Content.ToObjectFromJson<XeroTokenSet>();
    }

    public async Task DeleteAsync(string userId)
    {
        var blob = _container.GetBlobClient(BlobName(userId));
        await blob.DeleteIfExistsAsync();
    }

    private static string BlobName(string userId) => $"{userId}.json";
}
