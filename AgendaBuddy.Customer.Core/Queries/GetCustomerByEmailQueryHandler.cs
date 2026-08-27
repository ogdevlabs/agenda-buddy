namespace AgendaBuddy.Customer.Core.Queries;

// F-020-T12: moved from AgendaBuddy.EventAndCommands.Queries.Customers. Constructor takes only
// DI-resolvable services -- the pre-refactor handler took `email` as a per-instance constructor
// parameter; it now comes from the query.
public class GetCustomerByEmailQueryHandler(
    IMediator mediator,
    ICustomerService customerService,
    IEventStore eventStore)
    : IRequestHandler<GetCustomerByEmailQuery, Result<CustomerEntity>>
{
    public async Task<Result<CustomerEntity>> Handle(GetCustomerByEmailQuery request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        await mediator.Publish(new GetCustomerByEmailEvent { Email = request.Email }, cancellationToken);

        var customerEntity = await customerService.FindCustomerAsync(SupportTools<CustomerEntity>.FilterByEmail(request.Email));
        if (customerEntity is not null)
        {
            await eventStore.SaveAsync(QueryAudit.Success(nameof(GetCustomerByEmailQuery), 1));
            return Result.Ok(customerEntity);
        }

        await eventStore.SaveAsync(QueryAudit.Failure(nameof(GetCustomerByEmailQuery)));
        return Result.Fail<CustomerEntity>($"No customer found with email {request.Email}");
    }
}
