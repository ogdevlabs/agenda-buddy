namespace EventAndCommands.Queries.Provider;

public class GetProvidersQueryHandler(
    IMediator mediator,
    ProviderService providerService,
    IEventStore eventStore,
    PageRequest page)
    : IRequestHandler<GetProvidersQuery, PagedResponse<ProviderEntity>>
{
    /// <remarks>
    /// F-016-T15: takes a clamped <see cref="PageRequest"/> and pages at the database. The audit record
    /// counts the page actually disclosed, not the size of the collection.
    /// </remarks>
    public async Task<PagedResponse<ProviderEntity>> Handle(GetProvidersQuery request,
        CancellationToken cancellationToken)
    {
        await mediator.Publish(new GetAllProvidersEvent(), cancellationToken);

        var (items, totalCount) = await providerService.GetPagedProvidersAsync(page.Skip, page.PageSize);
        var providerEntities = items.ToList();

        await eventStore.SaveAsync(providerEntities.Count != 0
            ? QueryAudit.Success("GetProvidersQuery", providerEntities.Count)
            : QueryAudit.Failure("GetProvidersQuery"));

        return PagedResponse<ProviderEntity>.From(providerEntities, totalCount, page);
    }
}