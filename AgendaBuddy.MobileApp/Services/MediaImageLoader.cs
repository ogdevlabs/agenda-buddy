using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Routing;

namespace AgendaBuddy.MobileApp.Services;

/// <summary>
/// Cache first, then the authenticated media route. This is what every showcase image loads through, since a
/// plain URI image source cannot carry the caller's token.
/// </summary>
public sealed class MediaImageLoader
{
    private readonly IShowcaseApiService _api;
    private readonly MediaCache _cache;

    public MediaImageLoader(IShowcaseApiService api, MediaCache cache)
    {
        _api = api;
        _cache = cache;
    }

    public MediaCache Cache => _cache;

    /// <returns>The image bytes, or null when the image cannot be shown — the caller keeps its placeholder.</returns>
    public async Task<byte[]?> LoadAsync(
        string providerRef, string hash, MediaVariant variant, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerRef) || string.IsNullOrWhiteSpace(hash))
            return null;

        var cached = _cache.TryRead(hash, variant);
        if (cached is not null)
            return cached;

        var result = await _api.FetchImageAsync(providerRef, hash, variant, ct);
        if (result.IsSuccess && result.Value is { Length: > 0 } bytes)
        {
            _cache.Write(providerRef, hash, variant, bytes);
            return bytes;
        }

        // One 404 body covers an unknown image and a provider who is gone or hidden; either way nothing cached
        // for this image should be shown again.
        if (result.ErrorCode == ShowcaseErrorCodes.ShowcaseNotFound)
            _cache.Evict(hash);

        return null;
    }
}
