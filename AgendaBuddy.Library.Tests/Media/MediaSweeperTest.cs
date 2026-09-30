using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Media;
using AgendaBuddy.Library.Repositories;
using AgendaBuddy.Library.Showcase;
using MongoDB.Bson;
using Moq;
using Xunit;

namespace AgendaBuddy.Library.Tests.Media;

public class MediaSweeperTest
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IRepository<MediaRefEntity>> _mediaRefs = new();
    private readonly Mock<IRepository<ProviderEntity>> _providers = new();
    private readonly InMemoryBlobStore _blobStore = new();

    private MediaSweeper Build() => new(_mediaRefs.Object, _providers.Object, _blobStore, new FixedTimeProvider(Now));

    [Fact]
    public async Task SweepAsync_AStaleDetachedImage_RemovesItsRefAndBlobs()
    {
        var providerId = ObjectId.GenerateNewId();
        var stale = new MediaRefEntity { Id = ObjectId.GenerateNewId(), ProviderId = providerId, Hash = "h1" };
        var kept = MediaKeys.For(providerId, "h2", MediaKeys.Full);
        await _blobStore.PutAsync(MediaKeys.For(providerId, "h1", MediaKeys.Full), [1], "image/jpeg");
        await _blobStore.PutAsync(MediaKeys.For(providerId, "h1", MediaKeys.Thumb), [1], "image/jpeg");
        await _blobStore.PutAsync(kept, [1], "image/jpeg");
        _mediaRefs.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>())).ReturnsAsync([stale]);
        _mediaRefs.Setup(r => r.FindOneAndDeleteAsync(It.IsAny<BsonDocument>())).ReturnsAsync(stale);
        _providers.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>())).ReturnsAsync(new ProviderEntity());

        var summary = await Build().SweepAsync();

        Assert.Equal(new MediaSweepSummary(1, 0), summary);
        Assert.Equal([kept], await _blobStore.ListAsync(MediaKeys.Prefix(providerId)));
        _mediaRefs.Verify(r => r.FindOneAndDeleteAsync(It.Is<BsonDocument>(
            f => f["_id"] == stale.Id && f["attached"] == false)), Times.Once);
    }

    [Fact]
    public async Task SweepAsync_TheCutoffIsTwentyFourHoursAgo()
    {
        BsonDocument? filter = null;
        _mediaRefs.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>()))
                  .Callback<BsonDocument>(f => filter = f)
                  .ReturnsAsync([]);

        await Build().SweepAsync();

        Assert.NotNull(filter);
        Assert.False(filter["attached"].AsBoolean);
        var cutoff = filter["$or"][0]["detached_at"]["$lt"].ToUniversalTime();
        Assert.Equal(Now.UtcDateTime - MediaSweeper.DetachedRetention, cutoff);
    }

    [Fact]
    public async Task SweepAsync_AnImageReattachedSinceTheRead_IsKept()
    {
        var providerId = ObjectId.GenerateNewId();
        var key = MediaKeys.For(providerId, "h1", MediaKeys.Full);
        await _blobStore.PutAsync(key, [1], "image/jpeg");
        _mediaRefs.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>()))
                  .ReturnsAsync([new MediaRefEntity { Id = ObjectId.GenerateNewId(), ProviderId = providerId, Hash = "h1" }]);
        _mediaRefs.Setup(r => r.FindOneAndDeleteAsync(It.IsAny<BsonDocument>())).ReturnsAsync((MediaRefEntity?)null);
        _providers.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>())).ReturnsAsync(new ProviderEntity());

        var summary = await Build().SweepAsync();

        Assert.Equal(0, summary.DetachedRemoved);
        Assert.True(await _blobStore.ExistsAsync(key));
    }

    [Fact]
    public async Task SweepAsync_BlobsOfAProviderThatNoLongerExists_AreRemovedWithTheirRefs()
    {
        var gone = ObjectId.GenerateNewId();
        var present = ObjectId.GenerateNewId();
        await _blobStore.PutAsync(MediaKeys.For(gone, "h", MediaKeys.Full), [1], "image/jpeg");
        await _blobStore.PutAsync(MediaKeys.For(present, "h", MediaKeys.Full), [1], "image/jpeg");
        await _blobStore.PutAsync("not-an-id/h/full", [1], "image/jpeg");
        _mediaRefs.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>())).ReturnsAsync([]);
        _providers.Setup(r => r.FindOneAsync(It.Is<BsonDocument>(f => f["_id"] == gone)))
                  .ReturnsAsync((ProviderEntity?)null);
        _providers.Setup(r => r.FindOneAsync(It.Is<BsonDocument>(f => f["_id"] == present)))
                  .ReturnsAsync(new ProviderEntity());

        var summary = await Build().SweepAsync();

        Assert.Equal(1, summary.OrphanedPrefixesRemoved);
        Assert.Empty(await _blobStore.ListAsync(MediaKeys.Prefix(gone)));
        Assert.NotEmpty(await _blobStore.ListAsync(MediaKeys.Prefix(present)));
        Assert.True(await _blobStore.ExistsAsync("not-an-id/h/full"));
        _mediaRefs.Verify(r => r.DeleteManyAsync(It.Is<BsonDocument>(f => f["provider_id"] == gone)), Times.Once);
        _mediaRefs.Verify(r => r.DeleteManyAsync(It.Is<BsonDocument>(f => f["provider_id"] == present)), Times.Never);
    }
}
