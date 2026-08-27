namespace AgendaBuddy.Identity.Extensions;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public static class HttpContextExtensions
{
    private static readonly Microsoft.Net.Http.Headers.MediaTypeHeaderValue JsonMediaType = new("application/json");

    public static bool AcceptsJson(this HttpRequest httpRequest) => Accepts(httpRequest, JsonMediaType);

    public static bool Accepts(this HttpRequest httpRequest, Microsoft.Net.Http.Headers.MediaTypeHeaderValue mediaType)
    {
        if (httpRequest.GetTypedHeaders().Accept is { Count: > 0 } acceptHeader)
            return acceptHeader.Any(v => mediaType.IsSubsetOf(v));
        return false;
    }
}
