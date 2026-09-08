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

    /// <summary>
    /// Sets which avatar this account is drawn with, via a targeted <c>$set</c> on <c>avatar_id</c> alone.
    /// </summary>
    /// <remarks>
    /// A dedicated write rather than a field on the profile <c>PUT</c>, which replaces the whole document — so
    /// changing an avatar through it would carry every defect of a whole-document write, including discarding a
    /// concurrent change to the subscription or appointment lists.
    /// </remarks>
    /// <returns>The customer post-update, or <c>null</c> if <paramref name="customerEmail"/> matches no customer.</returns>
    Task<CustomerEntity?> SetAvatarAsync(string customerEmail, string avatarId);

    /// <summary>
    /// Records — or clears — this account's acceptance of the Terms and Conditions and the Privacy Policy.
    /// </summary>
    /// <param name="termsAcceptedAt">When the terms were confirmed, or <c>null</c> to record that they are not.</param>
    /// <param name="privacyAcceptedAt">When the policy was confirmed, or <c>null</c> to record that it is not.</param>
    /// <remarks>
    /// A targeted <c>$set</c>, for the same reason <see cref="SetAvatarAsync"/> is one. Both fields are always
    /// written, including to null: withdrawing consent is a state the record has to be able to express, and a
    /// method that could only ever add a timestamp would make an unticked box silently keep the old one.
    /// </remarks>
    /// <returns>The customer post-update, or <c>null</c> if <paramref name="customerEmail"/> matches no customer.</returns>
    Task<CustomerEntity?> SetConsentAsync(string customerEmail, DateTime? termsAcceptedAt, DateTime? privacyAcceptedAt);
}
