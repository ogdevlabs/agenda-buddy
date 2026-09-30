using AgendaBuddy.Library.Showcase;

namespace AgendaBuddy.Library.Media;

public enum MediaUploadStatus
{
    Created,
    Deduplicated,
    Rejected,
    RateLimited
}

public sealed record MediaUploadResult(
    MediaUploadStatus Status,
    string? Hash = null,
    int Width = 0,
    int Height = 0,
    string? Rejection = null,
    TimeSpan? RetryAfter = null);

public interface IMediaService
{
    Task<MediaUploadResult> UploadAsync(ProviderEntity provider, byte[] content, CancellationToken cancellationToken = default);

    /// <summary>
    /// The variant's bytes, or <c>null</c> for every reason the caller may not have them — one answer for unknown,
    /// detached, erased, deactivated, hidden and taken down, so the route cannot be used to tell them apart.
    /// </summary>
    Task<Stream?> OpenAsync(string providerRef, string hash, string variant, string callerEmail,
        CancellationToken cancellationToken = default);
}

public sealed class MediaService(
    IRepository<MediaRefEntity> mediaRefs,
    IShowcaseService showcaseService,
    IBlobStore blobStore,
    ImagePipeline pipeline,
    TimeProvider timeProvider)
    : IMediaService
{
    public const string ContentType = "image/jpeg";

    public async Task<MediaUploadResult> UploadAsync(
        ProviderEntity provider, byte[] content, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Counted before decoding, so a refused upload costs neither a decode nor a blob write.
        var retryAfter = await RetryAfterAsync(provider.Id, now);
        if (retryAfter is not null)
            return new MediaUploadResult(MediaUploadStatus.RateLimited, RetryAfter: retryAfter);

        var processed = await pipeline.ProcessAsync(content, cancellationToken);
        if (processed.Image is not { } image)
            return new MediaUploadResult(MediaUploadStatus.Rejected, Rejection: processed.Rejection);

        var existing = await FindAsync(provider.Id, image.Hash);
        if (existing is not null)
            return Deduplicated(existing);

        await blobStore.PutAsync(MediaKeys.For(provider.Id, image.Hash, MediaKeys.Full), image.Full, ContentType,
            cancellationToken);
        await blobStore.PutAsync(MediaKeys.For(provider.Id, image.Hash, MediaKeys.Thumb), image.Thumb, ContentType,
            cancellationToken);

        try
        {
            await mediaRefs.InsertAsync(new MediaRefEntity
            {
                Id = ObjectId.GenerateNewId(),
                ProviderId = provider.Id,
                Hash = image.Hash,
                Attached = false,
                Width = image.Width,
                Height = image.Height,
                Bytes = image.Full.LongLength + image.Thumb.LongLength,
                CreatedAt = now
            });
        }
        catch (MongoException ex) when (ShowcaseService.IsDuplicateKey(ex))
        {
            var raced = await FindAsync(provider.Id, image.Hash);
            if (raced is not null)
                return Deduplicated(raced);
            throw;
        }

        return new MediaUploadResult(MediaUploadStatus.Created, image.Hash, image.Width, image.Height);
    }

    public async Task<Stream?> OpenAsync(string providerRef, string hash, string variant, string callerEmail,
        CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(providerRef, out var providerId) || providerRef.Length != 24
            || !ShowcaseRules.IsHash(hash) || !MediaKeys.IsVariant(variant))
            return null;

        var media = await FindAsync(providerId, hash);
        if (media is null)
            return null;

        var provider = await showcaseService.FindProviderByRefAsync(providerRef);
        if (provider is null)
            return null;

        var isOwner = ShowcaseService.IsOwner(provider, callerEmail);
        if (!media.Attached && !isOwner)
            return null;

        var showcase = await showcaseService.FindShowcaseAsync(provider.Id);
        if (!await showcaseService.IsVisibleToAsync(provider, showcase, callerEmail))
            return null;

        return await blobStore.OpenReadAsync(MediaKeys.For(providerId, hash, variant), cancellationToken);
    }

    private async Task<TimeSpan?> RetryAfterAsync(ObjectId providerId, DateTime now)
    {
        var windowStart = now - TimeSpan.FromHours(1);
        var filter = new BsonDocument
        {
            { "provider_id", providerId },
            { "created_at", new BsonDocument("$gt", windowStart) }
        };

        var (_, count) = await mediaRefs.GetPagedAsync(filter, 0, 1);
        if (count < ShowcaseRules.UploadsPerHour)
            return null;

        var oldest = (await mediaRefs.FindAllAsync(filter, new BsonDocument("created_at", 1), 1)).FirstOrDefault();
        var wait = oldest is null ? TimeSpan.FromMinutes(1) : oldest.CreatedAt.AddHours(1) - now;
        return wait < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : wait;
    }

    private Task<MediaRefEntity?> FindAsync(ObjectId providerId, string hash) =>
        mediaRefs.FindOneAsync(new BsonDocument { { "provider_id", providerId }, { "hash", hash } });

    private static MediaUploadResult Deduplicated(MediaRefEntity media) =>
        new(MediaUploadStatus.Deduplicated, media.Hash, media.Width, media.Height);
}
