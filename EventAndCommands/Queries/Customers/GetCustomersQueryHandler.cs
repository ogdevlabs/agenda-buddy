namespace EventAndCommands.Queries.Customers;

public class GetCustomersQueryHandler(IMediator mediator, CustomerService customerService)
    : IRequestHandler<GetCustomersQuery, IEnumerable<CustomerEntity>>
{
    [InjectService] private IEventStore? EventStore { get; } = new EventStore();

    public async Task<IEnumerable<CustomerEntity>> Handle(GetCustomersQuery request,
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
            await EventStore!.SaveAsync(successEvent);
            return await Task.FromResult(customerEntities);
        }

        var failEvent = new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Failed",
            Type = "GetCustomersQuery",
            Data = JsonSerializer.Serialize(customerEntities.ToList())
        };
        await EventStore!.SaveAsync(failEvent);
        return await Task.FromResult(customerEntities);
    }
}