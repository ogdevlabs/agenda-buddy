namespace Library.Services;

public interface ICustomerService
{
    Task<IEnumerable<CustomerEntity>> GetAllCustomersAsync();
    Task AddCustomerAsync(CustomerEntity customerEntity);
    Task<bool> UpdateCustomerAsync(string id, CustomerEntity customerEntity);
    Task DeleteCustomerAsync(string id);
    Task<CustomerEntity> FindCustomerAsync(BsonDocument filter);
}