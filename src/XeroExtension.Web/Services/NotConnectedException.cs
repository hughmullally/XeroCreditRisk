namespace XeroExtension.Web.Services;

/// <summary>Thrown when a Xero API call is attempted before the user has completed the OAuth connect flow.</summary>
public class NotConnectedException(string message) : Exception(message);
