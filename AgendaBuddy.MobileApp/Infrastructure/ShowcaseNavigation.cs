using AgendaBuddy.MobileApp.Routing;

namespace AgendaBuddy.MobileApp.Infrastructure;

/// <summary>
/// The one shape every entry point opens a provider's showcase with, so the query keys the showcase page reads
/// cannot drift between the five surfaces that link to it.
/// </summary>
public static class ShowcaseNavigation
{
    public const string Route = "providerShowcase";

    public static Dictionary<string, object> Parameters(string providerRef, string? email, ShowcaseSource source)
    {
        var parameters = new Dictionary<string, object>
        {
            ["providerRef"] = providerRef,
            ["source"] = source
        };
        if (!string.IsNullOrWhiteSpace(email))
            parameters["email"] = email;
        return parameters;
    }
}

/// <summary>Raised by a view model that wants its page to open a provider's showcase.</summary>
public sealed class ShowcaseRequestedEventArgs(string providerRef, string? email, ShowcaseSource source) : EventArgs
{
    public string ProviderRef { get; } = providerRef;
    public string? Email { get; } = email;
    public ShowcaseSource Source { get; } = source;

    public Dictionary<string, object> Parameters => ShowcaseNavigation.Parameters(ProviderRef, Email, Source);
}
