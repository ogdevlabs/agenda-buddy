namespace Library.Services;

public interface ICustomerService
{
    Task<IEnumerable<CustomerEntity>> GetAllCustomers();
    Task AddCustomer(CustomerEntity customerEntity);
    Task<bool> UpdateCustomer(string id, CustomerEntity customerEntity);
    Task DeleteCustomer(string id);
    Task<CustomerEntity> FindCustomer(BsonDocument filter);
}