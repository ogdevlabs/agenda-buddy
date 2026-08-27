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
}
