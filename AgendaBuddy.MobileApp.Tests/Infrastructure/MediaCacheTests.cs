using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Routing;
using AgendaBuddy.MobileApp.Services;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

public sealed class MediaCacheTests : IDisposable
{
    private const string Provider = "66f1a2b3c4d5e6f708192a3b";
    private const string HashA = "aaaa1111";
    private const string HashB = "bbbb2222";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "media-cache-tests-" + Guid.NewGuid().ToString("N"));
    private DateTime _now = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

    private MediaCache Cache() => new(_root, () => _now);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void FilesLiveUnderMediaNamedByHashAndVariant()
    {
        var cache = Cache();

        Assert.Equal(Path.Combine(_root, "media", "aaaa1111_thumb"), cache.PathFor(HashA, MediaVariant.Thumb));
        Assert.Equal(Path.Combine(_root, "media", "aaaa1111_full"), cache.PathFor(HashA, MediaVariant.Full));
    }

    [Fact]
    public void AWrittenImageReadsBack()
    {
        var cache = Cache();
        cache.Write(Provider, HashA, MediaVariant.Thumb, [1, 2, 3]);

        Assert.Equal([1, 2, 3], cache.TryRead(HashA, MediaVariant.Thumb));
        Assert.Null(cache.TryRead(HashA, MediaVariant.Full));
    }

    [Fact]
    public void AnImageIsKeptForThirtyDaysAndThenDropped()
    {
        var cache = Cache();
        cache.Write(Provider, HashA, MediaVariant.Full, [9]);

        _now = _now.AddDays(29);
        Assert.NotNull(cache.TryRead(HashA, MediaVariant.Full));

        _now = _now.AddDays(2);
        Assert.Null(cache.TryRead(HashA, MediaVariant.Full));
        Assert.False(File.Exists(cache.PathFor(HashA, MediaVariant.Full)));
    }

    [Fact]
    public void EvictingAHashDropsBothVariants()
    {
        var cache = Cache();
        cache.Write(Provider, HashA, MediaVariant.Thumb, [1]);
        cache.Write(Provider, HashA, MediaVariant.Full, [2]);

        cache.Evict(HashA);

        Assert.Null(cache.TryRead(HashA, MediaVariant.Thumb));
        Assert.Null(cache.TryRead(HashA, MediaVariant.Full));
    }

    [Fact]
    public void EvictingAProviderDropsEveryImageCachedForThemAndNoOneElses()
    {
        var cache = Cache();
        cache.Write(Provider, HashA, MediaVariant.Thumb, [1]);
        cache.Write(Provider, HashA, MediaVariant.Thumb, [1]);
        cache.Write(Provider, HashB, MediaVariant.Full, [2]);
        cache.Write("77aa", "cccc3333", MediaVariant.Thumb, [3]);

        cache.EvictProvider(Provider);

        Assert.Null(cache.TryRead(HashA, MediaVariant.Thumb));
        Assert.Null(cache.TryRead(HashB, MediaVariant.Full));
        Assert.NotNull(cache.TryRead("cccc3333", MediaVariant.Thumb));
    }

    [Fact]
    public void AValueCannotNameAPathOutsideTheCache()
    {
        var cache = Cache();

        var path = cache.PathFor("../../etc/passwd", MediaVariant.Thumb);

        Assert.Equal(Path.Combine(_root, "media"), Path.GetDirectoryName(path));
    }

    [Fact]
    public async Task TheLoaderServesACachedImageWithoutAskingTheServer()
    {
        var cache = Cache();
        cache.Write(Provider, HashA, MediaVariant.Thumb, [7]);
        var api = new Mock<IShowcaseApiService>(MockBehavior.Strict);

        var bytes = await new MediaImageLoader(api.Object, cache).LoadAsync(Provider, HashA, MediaVariant.Thumb);

        Assert.Equal([7], bytes);
    }

    [Fact]
    public async Task TheLoaderFetchesAndCachesAMiss()
    {
        var cache = Cache();
        var api = new Mock<IShowcaseApiService>();
        api.Setup(a => a.FetchImageAsync(Provider, HashA, MediaVariant.Full, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseResult<byte[]>.Ok([4, 5]));
        var loader = new MediaImageLoader(api.Object, cache);

        Assert.Equal([4, 5], await loader.LoadAsync(Provider, HashA, MediaVariant.Full));
        Assert.Equal([4, 5], await loader.LoadAsync(Provider, HashA, MediaVariant.Full));

        api.Verify(a => a.FetchImageAsync(Provider, HashA, MediaVariant.Full, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ANotFoundImageEvictsAnyCachedCopy()
    {
        var cache = Cache();
        cache.Write(Provider, HashA, MediaVariant.Full, [1]);
        var api = new Mock<IShowcaseApiService>();
        api.Setup(a => a.FetchImageAsync(Provider, HashA, MediaVariant.Thumb, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseResult<byte[]>.Fail(ShowcaseErrorCodes.ShowcaseNotFound, 404));

        var bytes = await new MediaImageLoader(api.Object, cache).LoadAsync(Provider, HashA, MediaVariant.Thumb);

        Assert.Null(bytes);
        Assert.Null(cache.TryRead(HashA, MediaVariant.Full));
    }

    [Fact]
    public async Task ANetworkFailureKeepsTheCacheAndReturnsNothing()
    {
        var cache = Cache();
        cache.Write(Provider, HashB, MediaVariant.Full, [1]);
        var api = new Mock<IShowcaseApiService>();
        api.Setup(a => a.FetchImageAsync(Provider, HashA, MediaVariant.Thumb, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseResult<byte[]>.Fail(ShowcaseErrorCodes.Network, 0));

        Assert.Null(await new MediaImageLoader(api.Object, cache).LoadAsync(Provider, HashA, MediaVariant.Thumb));
        Assert.NotNull(cache.TryRead(HashB, MediaVariant.Full));
    }

    [Theory]
    [InlineData("", HashA)]
    [InlineData(Provider, "")]
    public async Task TheLoaderAsksNothingWithoutARefAndHash(string providerRef, string hash)
    {
        var api = new Mock<IShowcaseApiService>(MockBehavior.Strict);

        Assert.Null(await new MediaImageLoader(api.Object, Cache()).LoadAsync(providerRef, hash, MediaVariant.Thumb));
    }
}
