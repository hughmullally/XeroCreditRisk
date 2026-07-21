namespace XeroExtension.Web.Models;

public class XeroTokenSet
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public string[] Tenants { get; set; } = [];

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt.AddMinutes(-1);
}
