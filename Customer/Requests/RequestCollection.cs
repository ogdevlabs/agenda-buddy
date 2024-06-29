namespace Customer.Requests;

[ExcludeFromCodeCoverage]
public class RequestCollection(IKafkaClient kafkaClient) : IRequestCollection
{
    public async Task<string> AddCustomerRequest(IMediator mediator, CustomerService customerService,
        CustomerEntity customerEntity)
    {
        var result = await new AddCustomerCommandHandler(
                mediator,
                ((kafkaClient as KafkaClient)!),
                customerService,
                customerEntity)
            .Handle(
                new AddCustomerCommand { CustomerEntity = customerEntity },
                new CancellationToken());

        return result;
    }

    public async Task<string> UpdateCustomerRequest(string email, IMediator mediator, CustomerService customerService,
        CustomerEntity customerEntity)
    {
        var result =
            await new UpdateCustomerCommandHandler(email, mediator, customerService, customerEntity).Handle(
                new UpdateCustomerCommand { CustomerEntity = customerEntity }, new CancellationToken());
        return result;
    }

    public async Task<IEnumerable<CustomerEntity>> GetCustomersRequest(IMediator mediator,
        CustomerService customerService)
    {
        var result =
            await new GetCustomersQueryHandler(mediator, customerService).Handle(new GetCustomersQuery(),
                new CancellationToken());
        return result;
    }

    public async Task<CustomerEntity> GetCustomerByEmail(IMediator mediator, CustomerService customerService,
        string email)
    {
        var result =
            await new GetCustomerByEmailQueryHandler(mediator, customerService, email).Handle(
                new GetCustomerByEmailQuery(), new CancellationToken());
        return result;
    }
}