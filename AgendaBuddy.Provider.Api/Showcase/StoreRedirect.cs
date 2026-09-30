using System.Text;

namespace AgendaBuddy.Provider.Api.Showcase;

/// <summary>
/// Where a scanned QR sends a phone camera. The store URLs come from configuration only, never from the request,
/// so the route cannot be used as an open redirect. The fallback page is the same bytes for every code: it names no
/// provider and repeats no code.
/// </summary>
public static class StoreRedirect
{
    public const string AppStoreUrlKey = "Showcase:Go:AppStoreUrl";
    public const string PlayStoreUrlKey = "Showcase:Go:PlayStoreUrl";

    public const string Ios = "ios";
    public const string Android = "android";
    public const string Other = "other";

    /// <summary>An iPad in desktop mode reports <c>Macintosh</c>; the touch hint (<c>Mobile/</c>) is what tells it apart.</summary>
    public static string Classify(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return Other;
        if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
            return Android;
        if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("iPod", StringComparison.OrdinalIgnoreCase)
            || (userAgent.Contains("Macintosh", StringComparison.OrdinalIgnoreCase)
                && userAgent.Contains("Mobile/", StringComparison.OrdinalIgnoreCase)))
            return Ios;
        return Other;
    }

    public static string ChooserPage(string? appStoreUrl, string? playStoreUrl)
    {
        var links = new StringBuilder();
        if (IsHttps(appStoreUrl))
            links.Append($"<a href=\"{WebUtility.HtmlEncode(appStoreUrl)}\">App Store</a>");
        if (IsHttps(playStoreUrl))
            links.Append($"<a href=\"{WebUtility.HtmlEncode(playStoreUrl)}\">Google Play</a>");

        return $$"""
            <!doctype html>
            <html lang="es-MX">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="robots" content="noindex">
            <title>AgendaMe</title>
            <style>
            body{font-family:-apple-system,system-ui,sans-serif;margin:0;padding:48px 24px;text-align:center;color:#1b2b34;background:#f6f8f9}
            h1{font-size:28px;margin:0 0 24px}
            ol{text-align:left;max-width:420px;margin:0 auto 32px;line-height:1.6}
            a{display:inline-block;margin:8px;padding:14px 24px;border-radius:12px;background:#0f766e;color:#fff;text-decoration:none;font-weight:600}
            </style>
            </head>
            <body>
            <h1>Get AgendaMe</h1>
            <ol>
            <li>Escanea para descargar AgendaMe · Scan to get AgendaMe</li>
            <li>En la app, toca Escanear código — o escribe el código · In the app, tap Scan a code — or type the code</li>
            </ol>
            {{links}}
            </body>
            </html>
            """;
    }

    private static bool IsHttps(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    public static bool IsRedirectable(string? url) => IsHttps(url);
}
