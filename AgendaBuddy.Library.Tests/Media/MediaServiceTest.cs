using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Media;
using AgendaBuddy.Library.Repositories;
using AgendaBuddy.Library.Showcase;
using MongoDB.Bson;
using Moq;
using SkiaSharp;
using Xunit;

namespace AgendaBuddy.Library.Tests.Media;

public class MediaServiceTest
{
    private const string OwnerEmail = "coach@example.com";
    private const string ViewerEmail = "ada@example.com";
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IRepository<MediaRefEntity>> _mediaRefs = new();
    private readonly Mock<IShowcaseService> _showcaseService = new();
    private readonly InMemoryBlobStore _blobStore = new();
    private readonly ProviderEntity _provider = new()
    {
        Id = ObjectId.GenerateNewId(),
        FirstName = "Grace",
        LastName = "Hopper",
        Email = OwnerEmail
    };

    private MediaService Build() =>
        new(_mediaRefs.Object, _showcaseService.Object, _blobStore, new ImagePipeline(), new FixedTimeProvider(Now));

    private void UploadsInLastHour(long count, DateTime? oldest = null)
    {
        _mediaRefs.Setup(r => r.GetPagedAsync(It.IsAny<BsonDocument>(), 0, 1))
                  .ReturnsAsync((Enumerable.Empty<MediaRefEntity>(), count));
        _mediaRefs.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>(), 1))
                  .ReturnsAsync(oldest is null ? [] : [new MediaRefEntity { CreatedAt = oldest.Value }]);
    }

    private static string Hash(char c = 'a') => new(c, 64);

    [Fact]
    public async Task UploadAsync_AFreshImage_StoresBothVariantsAndARef()
    {
        UploadsInLastHour(0);
        MediaRefEntity? inserted = null;
        _mediaRefs.Setup(r => r.InsertAsync(It.IsAny<MediaRefEntity>()))
                  .Callback<MediaRefEntity>(m => inserted = m)
                  .Returns(Task.CompletedTask);

        var result = await Build().UploadAsync(_provider, TestImages.Encode(300, 200, SKEncodedImageFormat.Png));

        Assert.Equal(MediaUploadStatus.Created, result.Status);
        Assert.Equal(300, result.Width);
        Assert.Equal(200, result.Height);
        Assert.True(await _blobStore.ExistsAsync(MediaKeys.For(_provider.Id, result.Hash!, MediaKeys.Full)));
        Assert.True(await _blobStore.ExistsAsync(MediaKeys.For(_provider.Id, result.Hash!, MediaKeys.Thumb)));
        Assert.NotNull(inserted);
        Assert.False(inserted.Attached);
        Assert.Equal(_provider.Id, inserted.ProviderId);
        Assert.Equal(result.Hash, inserted.Hash);
        Assert.Equal(Now.UtcDateTime, inserted.CreatedAt);
    }

    [Fact]
    public async Task UploadAsync_AtTheHourlyLimit_IsRateLimitedWithoutDecodingOrWriting()
    {
        var oldest = Now.UtcDateTime.AddMinutes(-45);
        UploadsInLastHour(ShowcaseRules.UploadsPerHour, oldest);

        var result = await Build().UploadAsync(_provider, TestImages.Encode(10, 10, SKEncodedImageFormat.Png));

        Assert.Equal(MediaUploadStatus.RateLimited, result.Status);
        Assert.Equal(TimeSpan.FromMinutes(15), result.RetryAfter);
        Assert.Empty(await _blobStore.ListTopLevelPrefixesAsync());
        _mediaRefs.Verify(r => r.InsertAsync(It.IsAny<MediaRefEntity>()), Times.Never);
        _mediaRefs.Verify(r => r.FindOneAsync(It.IsAny<BsonDocument>()), Times.Never);
    }

    [Fact]
    public async Task UploadAsync_RateLimitedWithAnExpiringWindow_NeverAsksForLessThanOneSecond()
    {
        UploadsInLastHour(ShowcaseRules.UploadsPerHour, Now.UtcDateTime.AddHours(-1));

        var result = await Build().UploadAsync(_provider, [1, 2, 3]);

        Assert.Equal(TimeSpan.FromSeconds(1), result.RetryAfter);
    }

    [Fact]
    public async Task UploadAsync_JustUnderTheLimit_IsAccepted()
    {
        UploadsInLastHour(ShowcaseRules.UploadsPerHour - 1);

        var result = await Build().UploadAsync(_provider, TestImages.Encode(10, 10, SKEncodedImageFormat.Png));

        Assert.Equal(MediaUploadStatus.Created, result.Status);
    }

    [Fact]
    public async Task UploadAsync_AnImageTheProviderAlreadyHas_IsDeduplicatedWithoutWriting()
    {
        UploadsInLastHour(0);
        _mediaRefs.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>()))
                  .ReturnsAsync(new MediaRefEntity { Hash = Hash(), Width = 7, Height = 8 });

        var result = await Build().UploadAsync(_provider, TestImages.Encode(10, 10, SKEncodedImageFormat.Png));

        Assert.Equal(MediaUploadStatus.Deduplicated, result.Status);
        Assert.Equal(Hash(), result.Hash);
        Assert.Equal(7, result.Width);
        Assert.Equal(8, result.Height);
        Assert.Empty(await _blobStore.ListTopLevelPrefixesAsync());
        _mediaRefs.Verify(r => r.InsertAsync(It.IsAny<MediaRefEntity>()), Times.Never);
    }

    [Fact]
    public async Task UploadAsync_AnUnreadableFile_IsRejectedWithTheReason()
    {
        UploadsInLastHour(0);

        var result = await Build().UploadAsync(_provider, "not an image"u8.ToArray());

        Assert.Equal(MediaUploadStatus.Rejected, result.Status);
        Assert.Equal(ImageRejections.UnsupportedFormat, result.Rejection);
        _mediaRefs.Verify(r => r.InsertAsync(It.IsAny<MediaRefEntity>()), Times.Never);
    }

    [Fact]
    public async Task UploadAsync_StorageUnavailable_Propagates()
    {
        UploadsInLastHour(0);
        var service = new MediaService(_mediaRefs.Object, _showcaseService.Object, new UnavailableBlobStore(),
            new ImagePipeline(), new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<MediaStorageUnavailableException>(
            () => service.UploadAsync(_provider, TestImages.Encode(10, 10, SKEncodedImageFormat.Png)));
    }

    public static TheoryData<string, string, string> MalformedReferences => new()
    {
        { "not-an-object-id", new string('a', 64), MediaKeys.Full },
        { ObjectId.GenerateNewId().ToString(), new string('A', 64), MediaKeys.Full },
        { ObjectId.GenerateNewId().ToString(), new string('a', 63), MediaKeys.Full },
        { ObjectId.GenerateNewId().ToString(), "../../etc/passwd", MediaKeys.Full },
        { ObjectId.GenerateNewId().ToString(), new string('a', 64), "original" },
        { ObjectId.GenerateNewId().ToString(), new string('a', 64), "" }
    };

    [Theory]
    [MemberData(nameof(MalformedReferences))]
    public async Task OpenAsync_AMalformedReference_IsNullWithoutAnyLookup(string providerRef, string hash, string variant)
    {
        var stream = await Build().OpenAsync(providerRef, hash, variant, ViewerEmail);

        Assert.Null(stream);
        _mediaRefs.Verify(r => r.FindOneAsync(It.IsAny<BsonDocument>()), Times.Never);
        _showcaseService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OpenAsync_UnknownMedia_IsNull()
    {
        _mediaRefs.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>())).ReturnsAsync((MediaRefEntity?)null);

        Assert.Null(await Build().OpenAsync(_provider.Id.ToString(), Hash(), MediaKeys.Full, ViewerEmail));
    }

    [Fact]
    public async Task OpenAsync_AnErasedProvider_IsNull()
    {
        await ArrangeMedia(attached: true, visible: true);
        _showcaseService.Setup(s => s.FindProviderByRefAsync(It.IsAny<string?>())).ReturnsAsync((ProviderEntity?)null);

        Assert.Null(await Build().OpenAsync(_provider.Id.ToString(), Hash(), MediaKeys.Full, ViewerEmail));
    }

    [Fact]
    public async Task OpenAsync_DetachedMediaForSomeoneElse_IsNull()
    {
        await ArrangeMedia(attached: false, visible: true);

        Assert.Null(await Build().OpenAsync(_provider.Id.ToString(), Hash(), MediaKeys.Full, ViewerEmail));
    }

    [Fact]
    public async Task OpenAsync_DetachedMediaForTheOwner_ReturnsTheBytes()
    {
        await ArrangeMedia(attached: false, visible: true);

        await using var stream = await Build().OpenAsync(_provider.Id.ToString(), Hash(), MediaKeys.Full,
            OwnerEmail.ToUpperInvariant());

        Assert.NotNull(stream);
        Assert.Equal(new byte[] { 1, 2, 3 }, await ReadAll(stream));
    }

    [Fact]
    public async Task OpenAsync_AShowcaseNotVisibleToTheCaller_IsNull()
    {
        await ArrangeMedia(attached: true, visible: false);

        Assert.Null(await Build().OpenAsync(_provider.Id.ToString(), Hash(), MediaKeys.Full, ViewerEmail));
    }

    [Fact]
    public async Task OpenAsync_AttachedAndVisible_ReturnsTheRequestedVariant()
    {
        await ArrangeMedia(attached: true, visible: true);

        await using var stream = await Build().OpenAsync(_provider.Id.ToString(), Hash(), MediaKeys.Thumb, ViewerEmail);

        Assert.NotNull(stream);
        Assert.Equal(new byte[] { 4, 5 }, await ReadAll(stream));
    }

    private async Task ArrangeMedia(bool attached, bool visible)
    {
        _mediaRefs.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>()))
                  .ReturnsAsync(new MediaRefEntity { ProviderId = _provider.Id, Hash = Hash(), Attached = attached });
        _showcaseService.Setup(s => s.FindProviderByRefAsync(_provider.Id.ToString())).ReturnsAsync(_provider);
        _showcaseService.Setup(s => s.FindShowcaseAsync(_provider.Id)).ReturnsAsync(new ProviderShowcaseEntity());
        _showcaseService.Setup(s => s.IsVisibleToAsync(_provider, It.IsAny<ProviderShowcaseEntity?>(), It.IsAny<string>()))
                        .ReturnsAsync(visible);
        await _blobStore.PutAsync(MediaKeys.For(_provider.Id, Hash(), MediaKeys.Full), [1, 2, 3], MediaService.ContentType);
        await _blobStore.PutAsync(MediaKeys.For(_provider.Id, Hash(), MediaKeys.Thumb), [4, 5], MediaService.ContentType);
    }

    private static async Task<byte[]> ReadAll(Stream stream)
    {
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);
        return copy.ToArray();
    }
}
