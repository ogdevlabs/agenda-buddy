namespace AgendaBuddy.Customer.Core.Queries;

// Takes a PageRequest from the query rather than a per-instance constructor parameter.
//
// Always returns Result.Ok, even for an empty page: an empty customer list is a valid 200, not a
// failure. The audit record
// still distinguishes empty from non-empty (Failure vs Success) -- an AUDIT distinction only, not a
// control-flow one, exactly as it was before this move.
public class GetCustomersQueryHandler(
    IMediator mediator,
    ICustomerService customerService,
    IEventStore eventStore)
    : IRequestHandler<GetCustomersQuery, Result<PagedResponse<CustomerEntity>>>
{
    public async Task<Result<PagedResponse<CustomerEntity>>> Handle(GetCustomersQuery request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        await mediator.Publish(new GetAllCustomersEvent(), cancellationToken);

        var (items, totalCount) = await customerService.GetPagedCustomersAsync(request.Page.Skip, request.Page.PageSize);
        var customerEntities = items.ToList();

        await eventStore.SaveAsync(customerEntities.Count != 0
            ? QueryAudit.Success(nameof(GetCustomersQuery), customerEntities.Count)
            : QueryAudit.Failure(nameof(GetCustomersQuery)));

        return Result.Ok(PagedResponse<CustomerEntity>.From(customerEntities, totalCount, request.Page));
    }
}
