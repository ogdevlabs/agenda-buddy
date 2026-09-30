namespace AgendaBuddy.Library.Showcase;

/// <summary>The indexes the showcase's atomic writes depend on; created idempotently at startup.</summary>
/// <remarks>
/// Several of these are correctness, not speed: the unique <c>provider_id</c> is what makes the create-on-first-use
/// insert idempotent, and the unique <c>(provider_id, hash)</c> is what makes a concurrent duplicate upload
/// deduplicate instead of storing two refs.
/// </remarks>
public static class ShowcaseIndexes
{
    public static async Task CreateAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var unique = new CreateIndexOptions { Unique = true };

        await database.GetCollection<ProviderShowcaseEntity>(ShowcaseCollections.Showcases).Indexes.CreateManyAsync(
        [
            new CreateIndexModel<ProviderShowcaseEntity>(Keys<ProviderShowcaseEntity>("provider_id"), unique),
            new CreateIndexModel<ProviderShowcaseEntity>(Keys<ProviderShowcaseEntity>("public_code"),
                new CreateIndexOptions { Unique = true, Sparse = true })
        ], cancellationToken);

        await database.GetCollection<MediaRefEntity>(ShowcaseCollections.MediaRefs).Indexes.CreateManyAsync(
        [
            new CreateIndexModel<MediaRefEntity>(Keys<MediaRefEntity>("provider_id", "hash"), unique),
            new CreateIndexModel<MediaRefEntity>(Keys<MediaRefEntity>("attached", "created_at")),
            new CreateIndexModel<MediaRefEntity>(Keys<MediaRefEntity>("provider_id", "created_at"))
        ], cancellationToken);

        await database.GetCollection<ShowcaseVisitEntity>(ShowcaseCollections.Visits).Indexes.CreateOneAsync(
            new CreateIndexModel<ShowcaseVisitEntity>(Keys<ShowcaseVisitEntity>("provider_id", "customer_email"), unique),
            cancellationToken: cancellationToken);

        await database.GetCollection<GoCounterEntity>(ShowcaseCollections.GoCounters).Indexes.CreateOneAsync(
            new CreateIndexModel<GoCounterEntity>(Keys<GoCounterEntity>("code"), unique),
            cancellationToken: cancellationToken);

        await database.GetCollection<ShowcaseReportEntity>(ShowcaseCollections.Reports).Indexes.CreateOneAsync(
            new CreateIndexModel<ShowcaseReportEntity>(
                Keys<ShowcaseReportEntity>("reporter_email", "provider_id", "created_at")),
            cancellationToken: cancellationToken);

        await database.GetCollection<ShowcaseBlockEntity>(ShowcaseCollections.Blocks).Indexes.CreateOneAsync(
            new CreateIndexModel<ShowcaseBlockEntity>(Keys<ShowcaseBlockEntity>("customer_email", "provider_id"), unique),
            cancellationToken: cancellationToken);
    }

    private static IndexKeysDefinition<T> Keys<T>(params string[] fields)
    {
        var document = new BsonDocument();
        foreach (var field in fields)
            document.Add(field, 1);
        return new BsonDocumentIndexKeysDefinition<T>(document);
    }
}

public static class ShowcaseCollections
{
    public const string Showcases = "provider_showcase";
    public const string MediaRefs = "media_refs";
    public const string Visits = "showcase_visits";
    public const string GoCounters = "go_counters";
    public const string Reports = "showcase_reports";
    public const string Blocks = "showcase_blocks";
}
