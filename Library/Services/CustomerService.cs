namespace Library.Services;

public class CustomerService(IRepository<CustomerEntity> customerRepository) : ICustomerService
{
    public async Task<IEnumerable<CustomerEntity>> GetAllCustomers()
    {
        return await customerRepository.GetAllAsync();
    }

    public async Task AddCustomer(CustomerEntity customerEntity)
    {
        await customerRepository.InsertAsync(customerEntity);
    }

    public async Task<bool> UpdateCustomer(string id, CustomerEntity customerEntity)
    {
        var existingCustomer = await customerRepository.GetByIdAsync(id);
        if (existingCustomer == null) throw new ArgumentException("Customer Not Found");
        return await customerRepository.UpdateAsync(id, customerEntity);
    }

    public async Task DeleteCustomer(string id)
    {
        await customerRepository.DeleteAsync(id);
    }

    public async Task<CustomerEntity> FindCustomer(BsonDocument filter)
    {
        return await customerRepository.Find(filter);
    }
}