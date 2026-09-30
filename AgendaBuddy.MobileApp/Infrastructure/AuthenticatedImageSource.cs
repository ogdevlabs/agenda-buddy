using AgendaBuddy.MobileApp.Routing;

namespace AgendaBuddy.MobileApp.Infrastructure;

/// <summary>
/// One showcase image, named by the provider it belongs to and its content hash. Showcase images are served
/// only to an authenticated caller, so a plain URI image source cannot load them; this is what
/// <c>Controls/AuthenticatedImage</c> loads through <c>MediaImageLoader</c> instead.
/// </summary>
public sealed record AuthenticatedImageSource(string ProviderRef, string Hash, MediaVariant Variant)
{
    /// <returns>Null when either half is missing, which is the caller's cue to keep its placeholder.</returns>
    public static AuthenticatedImageSource? For(string? providerRef, string? hash, MediaVariant variant) =>
        string.IsNullOrWhiteSpace(providerRef) || string.IsNullOrWhiteSpace(hash)
            ? null
            : new AuthenticatedImageSource(providerRef.Trim(), hash.Trim(), variant);

    /// <summary>
    /// Whether an avatar draws the person's uploaded photo rather than their catalogue mark. A photo always
    /// supersedes the mark, but only once both the photo and the provider it belongs to are known.
    /// </summary>
    public static bool PhotoSupersedesMark(string? providerRef, string? photoHash) =>
        For(providerRef, photoHash, MediaVariant.Thumb) is not null;
}
