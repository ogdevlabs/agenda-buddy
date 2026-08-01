namespace EventAndCommands.Queries.Customers;

public class GetCustomerByEmailQueryHandler(IMediator mediator, CustomerService customerService, string email, IEventStore eventStore)
    : IRequestHandler<GetCustomerByEmailQuery, CustomerEntity>
{

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
            await eventStore.SaveAsync(successEvent);
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
        await eventStore.SaveAsync(failedEvent);
        return null!;
    }
}