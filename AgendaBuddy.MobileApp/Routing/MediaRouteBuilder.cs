namespace AgendaBuddy.MobileApp.Routing;

/// <summary>The two stored renditions of every uploaded image.</summary>
public enum MediaVariant
{
    Thumb,
    Full
}

/// <summary>Hosted by the Provider service under <c>/api/v1/media</c>.</summary>
public static class MediaRouteBuilder
{
    /// <summary>The raw image is the body, not multipart.</summary>
    public static RouteSpec Upload() => new(HttpMethod.Post, "api/v1/media");

    public static RouteSpec Fetch(string providerRef, string hash, MediaVariant variant) =>
        new(HttpMethod.Get,
            $"api/v1/media/{Uri.EscapeDataString(providerRef)}/{Uri.EscapeDataString(hash)}/{VariantValue(variant)}");

    public static string VariantValue(MediaVariant variant) => variant == MediaVariant.Full ? "full" : "thumb";
}
