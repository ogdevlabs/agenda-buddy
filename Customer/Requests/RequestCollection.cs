namespace Customer.Requests;

[ExcludeFromCodeCoverage]
public class RequestCollection(IKafkaClient kafkaClient, IEventStore eventStore) : IRequestCollection
{
    public async Task<string> AddCustomerRequest(IMediator mediator, CustomerService customerService,
        CustomerEntity customerEntity)
    {
        var result = await new AddCustomerCommandHandler(
                mediator,
                ((kafkaClient as KafkaClient)!),
                customerService,
                customerEntity,
                eventStore)
            .Handle(
                new AddCustomerCommand { CustomerEntity = customerEntity },
                new CancellationToken());

        return result;
    }

    public async Task<string> UpdateCustomerRequest(string email, IMediator mediator, CustomerService customerService,
        CustomerEntity customerEntity)
    {
        var result =
            await new UpdateCustomerCommandHandler(email, mediator, customerService, customerEntity, eventStore).Handle(
                new UpdateCustomerCommand { CustomerEntity = customerEntity }, new CancellationToken());
        return result;
    }

    public async Task<PagedResponse<CustomerEntity>> GetCustomersRequest(IMediator mediator,
        CustomerService customerService, PageRequest page)
    {
        var result =
            await new GetCustomersQueryHandler(mediator, customerService, eventStore, page).Handle(
                new GetCustomersQuery(), new CancellationToken());
        return result;
    }

    public async Task<CustomerEntity> GetCustomerByEmail(IMediator mediator, CustomerService customerService,
        string email)
    {
        var result =
            await new GetCustomerByEmailQueryHandler(mediator, customerService, email, eventStore).Handle(
                new GetCustomerByEmailQuery(), new CancellationToken());
        return result;
    }
}
