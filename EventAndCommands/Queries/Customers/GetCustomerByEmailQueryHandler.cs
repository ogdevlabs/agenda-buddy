namespace EventAndCommands.Queries.Customers;

[RegisterService(ServiceLifetime.Scoped)]
public class GetCustomerByEmailQueryHandler(IMediator mediator, CustomerService customerService, string email)
    : IRequestHandler<GetCustomerByEmailQuery, CustomerEntity>
{
    [InjectService] private IEventStore? EventStore { get; } = new EventStore();

    public async Task<CustomerEntity> Handle(GetCustomerByEmailQuery request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new GetCustomerByEmailEvent(), cancellationToken);
        var filterByEmail = SupportTools<CustomerEntity>.FilterByEmail(email);
        var matchedCustomer = await customerService.FindCustomerAsync(filterByEmail);
        if (matchedCustomer != null)
        {
            var successEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "GetCustomerByEmailQuery",
                Data = JsonSerializer.Serialize(matchedCustomer)
            };
            await EventStore!.SaveAsync(successEvent);
            return matchedCustomer;
        }
        var failedEvent = new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Failed",
            Type = "GetCustomerByEmailQuery",
            Data = JsonSerializer.Serialize(new CustomerEntity())
        };
        await EventStore!.SaveAsync(failedEvent);
        return null!;
    }
}