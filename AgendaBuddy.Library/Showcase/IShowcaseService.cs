namespace AgendaBuddy.Library.Showcase;

public interface IShowcaseService
{
    Task<ProviderEntity?> FindProviderByEmailAsync(string email);

    Task<ProviderEntity?> FindProviderByRefAsync(string? providerRef);

    Task<ProviderShowcaseEntity?> FindShowcaseAsync(ObjectId providerId);

    Task<ProviderShowcaseEntity?> FindShowcaseByCodeAsync(string code);

    /// <summary>Creates the provider's showcase on first use; idempotent and never an upsert.</summary>
    Task<ProviderShowcaseEntity> EnsureShowcaseAsync(ProviderEntity provider);

    Task<ProviderShowcaseEntity> SetTextAsync(ProviderEntity provider, string? tagline, string? about);

    /// <summary><paramref name="field"/> is <c>photo_hash</c> or <c>logo_hash</c>; a null hash clears it.</summary>
    Task<ShowcaseWriteResult> SetImageAsync(ProviderEntity provider, string field, string? hash);

    Task<ShowcaseWriteResult> AddPortfolioItemAsync(ProviderEntity provider, string hash, string? caption, string? serviceId);

    Task<ShowcaseWriteResult> UpdatePortfolioItemAsync(ProviderEntity provider, string hash, string? caption, string? serviceId);

    Task RemovePortfolioItemAsync(ProviderEntity provider, string hash);

    Task<ShowcaseWriteResult> ReorderPortfolioAsync(ProviderEntity provider, IReadOnlyList<string> hashes);

    Task<string> GetOrCreatePublicCodeAsync(ProviderEntity provider);

    ShowcaseCompleteness Completeness(ProviderShowcaseEntity showcase);

    Task<ShowcaseFunnel> GetFunnelAsync(ProviderEntity provider, ProviderShowcaseEntity showcase);

    /// <summary>Records a visit and returns the previous <c>last_seen_at</c> (null on first contact).</summary>
    Task<DateTime?> RecordVisitAsync(ObjectId providerId, string customerEmail, string source);

    Task<ShowcaseRelationship> GetRelationshipAsync(ProviderEntity provider, string callerEmail);

    Task<bool> IsBlockedAsync(string customerEmail, ObjectId providerId);

    Task<IReadOnlyList<ObjectId>> GetBlockedProviderIdsAsync(string customerEmail);

    Task BlockAsync(string customerEmail, ObjectId providerId);

    Task UnblockAsync(string customerEmail, ObjectId providerId);

    Task<IReadOnlyList<HiddenProvider>> GetHiddenProvidersAsync(string customerEmail);

    /// <summary>False when the same reporter already reported this provider within 24 hours.</summary>
    Task<bool> ReportAsync(ObjectId providerId, string reporterEmail, string reason, string? detail, string? portfolioHash);

    Task<IReadOnlyList<ShowcaseAvatarLookup>> LookupAsync(string callerEmail, bool callerIsProvider,
        IEnumerable<string> emails);

    Task<IReadOnlyDictionary<ObjectId, ShowcaseDirectoryEntry>> GetDirectoryEntriesAsync(IEnumerable<ObjectId> providerIds);

    /// <summary>An unknown code writes nothing; there is no upsert.</summary>
    Task IncrementGoCounterAsync(string? code, string platform);

    /// <summary>
    /// Whether <paramref name="callerEmail"/> may see this provider's showcase and media: an active provider, not
    /// taken down by the operator and not hidden by the caller. The owner always may.
    /// </summary>
    Task<bool> IsVisibleToAsync(ProviderEntity provider, ProviderShowcaseEntity? showcase, string callerEmail);
}
