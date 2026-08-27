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

    /// <summary>
    /// Adds <paramref name="customerEmail"/> to the provider's own <c>SubscribedCustomerCollection</c> —
    /// the reciprocal side of a customer's subscribe action, kept in sync via a targeted
    /// <c>$addToSet</c> (ADR-032). Also serves as the provider-existence check: a <c>null</c> return
    /// means <paramref name="providerEmail"/> matched no provider.
    /// </summary>
    Task<ProviderEntity?> SubscribeCustomerAsync(string providerEmail, string customerEmail);

    /// <summary>
    /// Removes <paramref name="customerEmail"/> from the provider's reciprocal subscriber list via a
    /// targeted <c>$pull</c>. A missing provider is not an error here — see the call site in
    /// <c>UnsubscribeFromProviderCommandHandler</c> for why cleanup on the customer's side must not be
    /// blocked by a since-deleted provider.
    /// </summary>
    Task<ProviderEntity?> UnsubscribeCustomerAsync(string providerEmail, string customerEmail);
}
