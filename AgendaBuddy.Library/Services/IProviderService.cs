namespace AgendaBuddy.Library.Services;

public interface IProviderService
{
    Task<IEnumerable<ProviderEntity>> GetAllProvidersAsync();

    /// <summary>One page of providers, plus the total number of them. F-016-T15 / ADR-023.</summary>
    Task<(IEnumerable<ProviderEntity> Items, long TotalCount)> GetPagedProvidersAsync(int skip, int take);

    Task<ProviderEntity> GetProviderByIdAsync(string id);
    Task AddProviderAsync(ProviderEntity provider);
    Task<bool> UpdateProviderAsync(string id, ProviderEntity provider);
    Task DeleteProviderAsync(string id);
    Task<ProviderEntity> FindProvidersAsync(BsonDocument filter);

    /// <summary>
    /// Flips a provider's active flag with a single targeted write. F-020-T11: added so
    /// DeactivateProviderCommandHandler can be typed against this interface rather than the concrete
    /// <see cref="ProviderService"/> class — the only two call sites (this one and
    /// <see cref="GetPagedProvidersAsync"/>) were the reason Provider's handlers were the first in the
    /// F-020 rollout unable to move to interface typing without this addition.
    /// </summary>
    Task<ProviderEntity?> SetActiveAsync(string providerEmail, bool isActive);
}
