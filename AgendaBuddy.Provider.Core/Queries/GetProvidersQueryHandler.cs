namespace AgendaBuddy.Provider.Core.Queries;

// F-020-T11: moved from AgendaBuddy.EventAndCommands.Queries.Provider. Takes a PageRequest from the
// query rather than a per-instance constructor parameter -- the pre-refactor handler
// (Requests/RequestCollection.cs, deleted) constructed this by hand, once per call, passing `page` into
// the constructor.
//
// Always returns Result.Ok, even for an empty page: preserved from Provider/Program.cs's pre-existing
// behaviour, which never treated an empty provider list as anything other than a 200. The audit record
// still distinguishes empty from non-empty (Failure vs Success) -- that is an AUDIT distinction only,
// not a control-flow one, exactly as it was before this move.
public class GetProvidersQueryHandler(
    IMediator mediator,
    IProviderService providerService,
    IEventStore eventStore)
    : IRequestHandler<GetProvidersQuery, Result<PagedResponse<ProviderEntity>>>
{
    public async Task<Result<PagedResponse<ProviderEntity>>> Handle(GetProvidersQuery request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        await mediator.Publish(new GetAllProvidersEvent(), cancellationToken);

        var (items, totalCount) = await providerService.GetPagedProvidersAsync(request.Page.Skip, request.Page.PageSize);
        var providerEntities = items.ToList();

        await eventStore.SaveAsync(providerEntities.Count != 0
            ? QueryAudit.Success(nameof(GetProvidersQuery), providerEntities.Count)
            : QueryAudit.Failure(nameof(GetProvidersQuery)));

        return Result.Ok(PagedResponse<ProviderEntity>.From(providerEntities, totalCount, request.Page));
    }
}
