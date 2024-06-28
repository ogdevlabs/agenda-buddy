namespace Customer.Requests;

public class RequestCollection : IRequestCollection
{
    public async Task<string> AddCustomerRequest(IMediator mediator, CustomerService customerService,
        CustomerEntity customerEntity)
    {
        throw new NotImplementedException();
    }

    public async Task<string> UpdateCustomerRequest(IMediator mediator, CustomerService customerService,
        CustomerEntity customerEntity)
    {
        throw new NotImplementedException();
    }

    public async Task<IEnumerable<CustomerEntity>> GetCustomersRequest(IMediator mediator,
        CustomerService customerService, CustomerEntity customerEntity)
    {
        throw new NotImplementedException();
    }

    public async Task<CustomerEntity> GetCustomerByEmail(IMediator mediator, CustomerService customerService,
        string email)
    {
        throw new NotImplementedException();
    }
}