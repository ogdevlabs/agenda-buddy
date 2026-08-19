namespace EventAndCommands.Queries.Customers;

public class GetCustomersQueryHandler(
    IMediator mediator,
    CustomerService customerService,
    IEventStore eventStore,
    PageRequest page)
    : IRequestHandler<GetCustomersQuery, PagedResponse<CustomerEntity>>
{
    /// <remarks>See <c>GetProvidersQueryHandler</c>. F-016-T15.</remarks>
    public async Task<PagedResponse<CustomerEntity>> Handle(GetCustomersQuery request,
        CancellationToken cancellationToken)
    {
        await mediator.Publish(new GetAllCustomersEvent(), cancellationToken);

        var (items, totalCount) = await customerService.GetPagedCustomersAsync(page.Skip, page.PageSize);
        var customerEntities = items.ToList();

        await eventStore.SaveAsync(customerEntities.Count != 0
            ? QueryAudit.Success("GetCustomersQuery", customerEntities.Count)
            : QueryAudit.Failure("GetCustomersQuery"));

        return PagedResponse<CustomerEntity>.From(customerEntities, totalCount, page);
    }
}