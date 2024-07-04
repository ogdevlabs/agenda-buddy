namespace Customer.Requests;

public interface IRequestCollection
{
    public Task<string> AddCustomerRequest(IMediator mediator, CustomerService customerService,
        CustomerEntity customerEntity);

    public Task<string> UpdateCustomerRequest(string email, IMediator mediator, CustomerService customerService,
        CustomerEntity customerEntity);

    public Task<List<CustomerEntity>> GetCustomersRequest(IMediator mediator, CustomerService customerService);

    public Task<CustomerEntity> GetCustomerByEmail(IMediator mediator, CustomerService customerService, string email);
}