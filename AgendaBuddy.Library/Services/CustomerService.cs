namespace AgendaBuddy.Library.Services;

public class CustomerService(IRepository<CustomerEntity> customerRepository) : ICustomerService
{
    public async Task<IEnumerable<CustomerEntity>> GetAllCustomersAsync()
    {
        return await customerRepository.GetAllAsync();
    }

    /// <summary>
    /// One page of customers, plus the total number of them. ADR-023.
    /// </summary>
    /// <remarks>See <c>ProviderService.GetPagedProvidersAsync</c> for why this is paged at the database.</remarks>
    public async Task<(IEnumerable<CustomerEntity> Items, long TotalCount)> GetPagedCustomersAsync(int skip, int take)
    {
        return await customerRepository.GetPagedAsync(skip, take);
    }

    public async Task AddCustomerAsync(CustomerEntity customerEntity)
    {
        await customerRepository.InsertAsync(customerEntity);
    }

    public async Task<bool> UpdateCustomerAsync(string id, CustomerEntity customerEntity)
    {
        var existingCustomer = await customerRepository.GetByIdAsync(id);
        if (existingCustomer == null) throw new ArgumentException("Customer Not Found");
        return await customerRepository.UpdateAsync(id, customerEntity);
    }

    public async Task DeleteCustomerAsync(string id)
    {
        await customerRepository.DeleteAsync(id);
    }

    public async Task<CustomerEntity> FindCustomerAsync(BsonDocument filter)
    {
        return await customerRepository.Find(filter);
    }

    public async Task<CustomerEntity?> SubscribeToProviderAsync(string customerEmail, string providerEmail)
    {
        var filter = SupportTools<CustomerEntity>.FilterByEmail(customerEmail);
        var update = new BsonDocument("$addToSet", new BsonDocument("subscribed_provider_collection", providerEmail));
        return await customerRepository.FindOneAndUpdateAsync(filter, update);
    }

    public async Task<CustomerEntity?> UnsubscribeFromProviderAsync(string customerEmail, string providerEmail)
    {
        var filter = SupportTools<CustomerEntity>.FilterByEmail(customerEmail);
        var update = new BsonDocument("$pull", new BsonDocument("subscribed_provider_collection", providerEmail));
        return await customerRepository.FindOneAndUpdateAsync(filter, update);
    }

    public async Task<CustomerEntity?> SetAvatarAsync(string customerEmail, string avatarId)
    {
        return await customerRepository.FindOneAndUpdateAsync(
            SupportTools<CustomerEntity>.FilterByEmail(customerEmail),
            new BsonDocument("$set", new BsonDocument("avatar_id", avatarId)));
    }

    public async Task<CustomerEntity?> SetConsentAsync(
        string customerEmail, DateTime? termsAcceptedAt, DateTime? privacyAcceptedAt)
    {
        return await customerRepository.FindOneAndUpdateAsync(
            SupportTools<CustomerEntity>.FilterByEmail(customerEmail),
            new BsonDocument("$set", new BsonDocument
            {
                { "terms_accepted_at", ConsentTimestamp(termsAcceptedAt) },
                { "privacy_accepted_at", ConsentTimestamp(privacyAcceptedAt) }
            }));
    }

    public async Task<CustomerEntity?> SetPaymentCustomerAsync(string customerEmail, string stripeCustomerId)
    {
        return await customerRepository.FindOneAndUpdateAsync(
            SupportTools<CustomerEntity>.FilterByEmail(customerEmail),
            new BsonDocument("$set", new BsonDocument("stripe_customer_id", stripeCustomerId)));
    }

    public async Task<CustomerEntity?> FindCustomerByStripeCustomerIdAsync(string stripeCustomerId) =>
        await customerRepository.FindOneAsync(new BsonDocument("stripe_customer_id", stripeCustomerId));

    public async Task<CustomerEntity?> SetPaymentMethodAsync(
        string customerEmail, string stripeCustomerId, SavedPaymentMethod paymentMethod)
    {
        return await customerRepository.FindOneAndUpdateAsync(
            SupportTools<CustomerEntity>.FilterByEmail(customerEmail),
            new BsonDocument("$set", new BsonDocument
            {
                { "stripe_customer_id", stripeCustomerId },
                { "stripe_default_payment_method_id", paymentMethod.PaymentMethodId },
                { "payment_method_type", paymentMethod.Type },
                { "payment_method_brand", paymentMethod.Brand is null ? BsonNull.Value : paymentMethod.Brand },
                { "payment_method_last4", paymentMethod.Last4 is null ? BsonNull.Value : paymentMethod.Last4 }
            }));
    }

    /// <summary>
    /// A consent timestamp as BSON — an explicit null for "not accepted", not an omitted field.
    /// </summary>
    /// <remarks>
    /// Omitting it would leave a previous acceptance standing, so an unticked box would look accepted on the next
    /// read. The entity's <c>[BsonIgnoreIfNull]</c> governs serialising the whole document; this write is a
    /// <c>$set</c> and has to state the null itself.
    /// </remarks>
    private static BsonValue ConsentTimestamp(DateTime? at) =>
        at is null ? BsonNull.Value : new BsonDateTime(at.Value);
}
