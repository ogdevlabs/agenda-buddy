namespace AgendaBuddy.Library.Services;

public interface IProviderService
{
    Task<IEnumerable<ProviderEntity>> GetAllProvidersAsync();

    /// <summary>One page of providers, plus the total number of them. ADR-023.</summary>
    Task<(IEnumerable<ProviderEntity> Items, long TotalCount)> GetPagedProvidersAsync(int skip, int take);

    Task<ProviderEntity> GetProviderByIdAsync(string id);
    Task AddProviderAsync(ProviderEntity provider);
    Task<bool> UpdateProviderAsync(string id, ProviderEntity provider);
    Task DeleteProviderAsync(string id);
    Task<ProviderEntity> FindProvidersAsync(BsonDocument filter);

    /// <summary>
    /// Flips a provider's active flag with a single targeted write. Added so
    /// DeactivateProviderCommandHandler can be typed against this interface rather than the concrete
    /// <see cref="ProviderService"/> class — the only two call sites (this one and
    /// <see cref="GetPagedProvidersAsync"/>) were the reason Provider's handlers could not move to
    /// interface typing without this addition.
    /// </summary>
    Task<ProviderEntity?> SetActiveAsync(string providerEmail, bool isActive);
}
