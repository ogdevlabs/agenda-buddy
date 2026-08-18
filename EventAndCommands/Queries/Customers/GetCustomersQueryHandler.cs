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
            await eventStore.SaveAsync(QueryAudit.Success("GetCustomersQuery", customerEntities.Count()));
            return customerEntities;
        }

        await eventStore.SaveAsync(QueryAudit.Failure("GetCustomersQuery"));
        return customerEntities;
    }
}