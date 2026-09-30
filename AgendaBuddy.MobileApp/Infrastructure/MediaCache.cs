using AgendaBuddy.MobileApp.Routing;

namespace AgendaBuddy.MobileApp.Infrastructure;

/// <summary>
/// The on-disk copy of showcase images, at <c>{root}/media/{hash}_{variant}</c>.
/// </summary>
/// <remarks>
/// <para>
/// A hash names its bytes for ever, so a cached file can only go stale by its owner disappearing — which is why
/// the TTL is long (30 days) and why a showcase answering 404 evicts every image this device has cached for it.
/// The server's <c>immutable</c> header would allow a year; the TTL is what bounds how long an erased or hidden
/// provider's work can outlive them on a phone that never opens their showcase again.
/// </para>
/// <para>
/// Which hashes belong to which provider is recorded in a small index file per provider, so eviction does not
/// need the showcase that just answered 404 to say what it contained.
/// </para>
/// </remarks>
public sealed class MediaCache
{
    public static readonly TimeSpan DefaultTimeToLive = TimeSpan.FromDays(30);

    private readonly string _directory;
    private readonly Func<DateTime> _utcNow;
    private readonly TimeSpan _timeToLive;
    private readonly object _indexLock = new();

    public MediaCache(string rootDirectory, Func<DateTime>? utcNow = null, TimeSpan? timeToLive = null)
    {
        _directory = Path.Combine(rootDirectory, "media");
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _timeToLive = timeToLive ?? DefaultTimeToLive;
    }

    public string PathFor(string hash, MediaVariant variant) =>
        Path.Combine(_directory, $"{Sanitise(hash)}_{MediaRouteBuilder.VariantValue(variant)}");

    /// <summary>The cached bytes, or null when absent or older than the TTL. An expired file is deleted.</summary>
    public byte[]? TryRead(string hash, MediaVariant variant)
    {
        var path = PathFor(hash, variant);
        try
        {
            if (!File.Exists(path))
                return null;

            if (_utcNow() - File.GetLastWriteTimeUtc(path) > _timeToLive)
            {
                File.Delete(path);
                return null;
            }

            return File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Write(string providerRef, string hash, MediaVariant variant, byte[] bytes)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            var path = PathFor(hash, variant);
            var temporary = path + ".tmp";
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, path, overwrite: true);
            File.SetLastWriteTimeUtc(path, _utcNow());
            Remember(providerRef, hash);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public void Evict(string hash)
    {
        foreach (var variant in Enum.GetValues<MediaVariant>())
            TryDelete(PathFor(hash, variant));
    }

    /// <summary>Drops every image cached for a provider whose showcase is gone for this caller.</summary>
    public void EvictProvider(string providerRef)
    {
        if (string.IsNullOrWhiteSpace(providerRef))
            return;

        var index = IndexPath(providerRef);
        lock (_indexLock)
        {
            try
            {
                if (File.Exists(index))
                {
                    foreach (var hash in File.ReadAllLines(index).Where(l => l.Length > 0))
                        Evict(hash);
                }
            }
            catch (IOException)
            {
            }

            TryDelete(index);
        }
    }

    private void Remember(string providerRef, string hash)
    {
        if (string.IsNullOrWhiteSpace(providerRef))
            return;

        var index = IndexPath(providerRef);
        lock (_indexLock)
        {
            var known = File.Exists(index) ? File.ReadAllLines(index) : [];
            if (!known.Contains(Sanitise(hash), StringComparer.Ordinal))
                File.AppendAllLines(index, [Sanitise(hash)]);
        }
    }

    private string IndexPath(string providerRef) => Path.Combine(_directory, $"{Sanitise(providerRef)}.index");

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    // Hashes and refs are hex from the server; anything else is reduced to hex so a value can never name a path.
    private static string Sanitise(string value) =>
        new(value.Where(Uri.IsHexDigit).Select(char.ToLowerInvariant).ToArray());
}
