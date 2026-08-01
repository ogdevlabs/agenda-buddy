namespace EventAndCommands.Queries.Customers;

public class GetCustomersQueryHandler(IMediator mediator, CustomerService customerService, IEventStore eventStore)
    : IRequestHandler<GetCustomersQuery, List<CustomerEntity>>
{

    public async Task<List<CustomerEntity>> Handle(GetCustomersQuery request,
        CancellationToken cancellationToken)
    {
        await mediator.Publish(new GetAllCustomersEvent(), cancellationToken);
        var customerList = await customerService.GetAllCustomersAsync();
        var customerEntities = customerList.ToList();
        if (customerEntities.ToList().Count != 0)
        {
            var successEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "GetCustomersQuery",
                Data = JsonSerializer.Serialize(customerEntities.ToList())
            };
            await eventStore.SaveAsync(successEvent);
            return customerEntities;
        }

        var failEvent = new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Failed",
            Type = "GetCustomersQuery",
            Data = JsonSerializer.Serialize(customerEntities.ToList())
        };
        await eventStore.SaveAsync(failEvent);
        return customerEntities;
    }
}