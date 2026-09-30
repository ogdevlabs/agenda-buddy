using AgendaBuddy.Library.Showcase;

namespace AgendaBuddy.Library.Media;

public sealed record MediaSweepSummary(int DetachedRemoved, int OrphanedPrefixesRemoved);

/// <summary>
/// Removes what nothing references any more: images detached for longer than the undo window, and every blob
/// under a provider id that no longer has a provider document.
/// </summary>
/// <remarks>
/// The second half is what makes account erasure complete. Erasure deletes the blob prefix best-effort only — it
/// must answer 204 even when storage is down — so a failed delete there is finished here, on the next run.
/// </remarks>
public sealed class MediaSweeper(
    IRepository<MediaRefEntity> mediaRefs,
    IRepository<ProviderEntity> providers,
    IBlobStore blobStore,
    TimeProvider timeProvider)
{
    public static readonly TimeSpan DetachedRetention = TimeSpan.FromHours(24);

    public async Task<MediaSweepSummary> SweepAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = timeProvider.GetUtcNow().UtcDateTime - DetachedRetention;

        var stale = await mediaRefs.FindAllAsync(new BsonDocument
        {
            { "attached", false },
            {
                "$or", new BsonArray
                {
                    new BsonDocument("detached_at", new BsonDocument("$lt", cutoff)),
                    new BsonDocument
                    {
                        { "detached_at", BsonNull.Value },
                        { "created_at", new BsonDocument("$lt", cutoff) }
                    }
                }
            }
        });

        var detachedRemoved = 0;
        foreach (var media in stale)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // The filter repeats attached=false so an image re-attached since the read above is kept.
            var removed = await mediaRefs.FindOneAndDeleteAsync(new BsonDocument
            {
                { "_id", media.Id },
                { "attached", false }
            });
            if (removed is null)
                continue;

            await blobStore.DeletePrefixAsync($"{media.ProviderId}/{media.Hash}/", cancellationToken);
            detachedRemoved++;
        }

        var orphansRemoved = 0;
        foreach (var prefix in await blobStore.ListTopLevelPrefixesAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var segment = prefix.TrimEnd('/');
            if (!ObjectId.TryParse(segment, out var providerId))
                continue;

            if (await providers.FindOneAsync(new BsonDocument("_id", providerId)) is not null)
                continue;

            await blobStore.DeletePrefixAsync(MediaKeys.Prefix(providerId), cancellationToken);
            await mediaRefs.DeleteManyAsync(new BsonDocument("provider_id", providerId));
            orphansRemoved++;
        }

        return new MediaSweepSummary(detachedRemoved, orphansRemoved);
    }
}
