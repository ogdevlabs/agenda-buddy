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
            await eventStore.SaveAsync(QueryAudit.Success("GetCustomerByEmailQuery", 1));
            return matchedCustomer;
        }
        await eventStore.SaveAsync(QueryAudit.Failure("GetCustomerByEmailQuery"));
        return null!;
    }
}
