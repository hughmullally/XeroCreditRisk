namespace XeroExtension.Web.Services;

/// <summary>In-memory pub-sub so the webhook receiver can tell any open dashboard tabs that something changed.</summary>
public class DashboardNotifier
{
    public event Action? Changed;

    public void NotifyChanged() => Changed?.Invoke();
}
