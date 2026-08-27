namespace AgendaBuddy.Library.Services;

public interface ICustomerService
{
    Task<IEnumerable<CustomerEntity>> GetAllCustomersAsync();

    /// <summary>
    /// One page of customers, plus the total number of them. ADR-023. Added to the
    /// interface so <c>GetCustomersQueryHandler</c> can be typed against
    /// <see cref="ICustomerService"/> rather than the concrete <see cref="CustomerService"/> class —
    /// the same gap <c>IProviderService.SetActiveAsync</c> closed for Provider.
    /// </summary>
    Task<(IEnumerable<CustomerEntity> Items, long TotalCount)> GetPagedCustomersAsync(int skip, int take);

    Task AddCustomerAsync(CustomerEntity customerEntity);
    Task<bool> UpdateCustomerAsync(string id, CustomerEntity customerEntity);
    Task DeleteCustomerAsync(string id);
    Task<CustomerEntity> FindCustomerAsync(BsonDocument filter);

    /// <summary>
    /// Adds <paramref name="providerEmail"/> to the customer's subscription list via a targeted
    /// <c>$addToSet</c> (ADR-032's partial-update primitive) — atomic, and naturally idempotent:
    /// subscribing twice does not duplicate the entry.
    /// </summary>
    /// <returns>The customer post-update, or <c>null</c> if <paramref name="customerEmail"/> matches no customer.</returns>
    Task<CustomerEntity?> SubscribeToProviderAsync(string customerEmail, string providerEmail);

    /// <summary>
    /// Removes <paramref name="providerEmail"/> from the customer's subscription list via a targeted
    /// <c>$pull</c>. Unsubscribing from a provider that was never subscribed to is a no-op, not an error.
    /// </summary>
    /// <returns>The customer post-update, or <c>null</c> if <paramref name="customerEmail"/> matches no customer.</returns>
    Task<CustomerEntity?> UnsubscribeFromProviderAsync(string customerEmail, string providerEmail);
}
