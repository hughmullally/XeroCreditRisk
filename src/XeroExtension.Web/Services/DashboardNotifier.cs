namespace XeroExtension.Web.Services;

/// <summary>In-memory pub-sub so the webhook receiver can tell any open dashboard tabs that something changed.</summary>
public class DashboardNotifier
{
    public event Action<IReadOnlyList<string>>? Changed;

    /// <param name="contactIds">Contacts affected by the change, so the dashboard can highlight just those rows.</param>
    public void NotifyChanged(IReadOnlyList<string> contactIds) => Changed?.Invoke(contactIds);
}
