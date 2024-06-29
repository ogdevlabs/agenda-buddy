namespace Library.Services;

public class CustomerService(IRepository<CustomerEntity> customerRepository) : ICustomerService
{
    public async Task<IEnumerable<CustomerEntity>> GetAllCustomersAsync()
    {
        return await customerRepository.GetAllAsync();
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
}