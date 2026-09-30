namespace AgendaBuddy.MobileApp.Routing;

/// <summary>Where a showcase visit started. Recorded by the server for the provider's funnel.</summary>
public enum ShowcaseSource
{
    Directory,
    Scan,
    Code,
    Appointment,
    Message,
    Booking
}

/// <summary>
/// Hosted by the Provider service under <c>/api/v1/showcase</c>. Every authoring route is keyed by the caller's
/// own token (<c>/me/…</c>), so nothing here takes an address.
/// </summary>
public static class ShowcaseRouteBuilder
{
    private const string Root = "api/v1/showcase";

    public static RouteSpec Mine() => new(HttpMethod.Get, $"{Root}/me");

    public static RouteSpec SetText() => new(HttpMethod.Put, $"{Root}/me/text");

    public static RouteSpec SetPhoto() => new(HttpMethod.Put, $"{Root}/me/photo");

    public static RouteSpec SetLogo() => new(HttpMethod.Put, $"{Root}/me/logo");

    public static RouteSpec AddPortfolioItem() => new(HttpMethod.Post, $"{Root}/me/portfolio");

    public static RouteSpec UpdatePortfolioItem(string hash) =>
        new(HttpMethod.Patch, $"{Root}/me/portfolio/{Uri.EscapeDataString(hash)}");

    public static RouteSpec RemovePortfolioItem(string hash) =>
        new(HttpMethod.Delete, $"{Root}/me/portfolio/{Uri.EscapeDataString(hash)}");

    public static RouteSpec ReorderPortfolio() => new(HttpMethod.Put, $"{Root}/me/portfolio/order");

    /// <summary>Get-or-create: the same code comes back on every call.</summary>
    public static RouteSpec PublicCode() => new(HttpMethod.Post, $"{Root}/me/code");

    public static RouteSpec View(string providerRef, ShowcaseSource source) =>
        new(HttpMethod.Get, $"{Root}/{Uri.EscapeDataString(providerRef)}?source={SourceValue(source)}");

    public static RouteSpec ByCode(string code, ShowcaseSource source) =>
        new(HttpMethod.Get, $"{Root}/by-code/{Uri.EscapeDataString(code)}?source={SourceValue(source)}");

    /// <summary>A POST so the addresses travel in the body, never the URL.</summary>
    public static RouteSpec Lookup() => new(HttpMethod.Post, $"{Root}/lookup");

    public static RouteSpec Report(string providerRef) =>
        new(HttpMethod.Post, $"{Root}/{Uri.EscapeDataString(providerRef)}/report");

    public static RouteSpec Hide(string providerRef) =>
        new(HttpMethod.Put, $"{Root}/{Uri.EscapeDataString(providerRef)}/hide");

    public static RouteSpec Unhide(string providerRef) =>
        new(HttpMethod.Delete, $"{Root}/{Uri.EscapeDataString(providerRef)}/hide");

    public static RouteSpec Hidden() => new(HttpMethod.Get, $"{Root}/hidden");

    public static string SourceValue(ShowcaseSource source) => source switch
    {
        ShowcaseSource.Scan => "scan",
        ShowcaseSource.Code => "code",
        ShowcaseSource.Appointment => "appointment",
        ShowcaseSource.Message => "message",
        ShowcaseSource.Booking => "booking",
        _ => "directory"
    };
}
