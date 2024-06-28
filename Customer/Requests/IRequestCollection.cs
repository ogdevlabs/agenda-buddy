namespace Customer.Requests;

public interface IRequestCollection
{
    public Task<string> AddCustomerRequest(IMediator mediator, CustomerService customerService,
        CustomerEntity customerEntity);

    public Task<string> UpdateCustomerRequest(IMediator mediator, CustomerService customerService,
        CustomerEntity customerEntity);

    public Task<IEnumerable<CustomerEntity>> GetCustomersRequest(IMediator mediator, CustomerService customerService,
        CustomerEntity customerEntity);

    public Task<CustomerEntity> GetCustomerByEmail(IMediator mediator, CustomerService customerService, string email);
}