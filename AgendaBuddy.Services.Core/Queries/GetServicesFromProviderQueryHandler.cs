namespace AgendaBuddy.Services.Core.Queries;

// Constructor takes only DI-resolvable services; the per-request email comes from the query, not a
// constructor parameter. Typed against IProviderService, not the concrete class: it already covers
// everything this handler calls.
//
// A missing provider is a successful EMPTY read here, not a failure -- its null-check on the result
// never actually fires, because this handler already returns an empty, non-null list on that branch.
// Deliberately not changed to Result.Fail (which is what Calendar's equivalent does): that would flip
// this route from 200-with-empty-array to 404 for a caller checking their own missing profile, a real
// behaviour change.
public class GetServicesFromProviderQueryHandler(
    IMediator mediator,
    IProviderService providerService,
    IEventStore eventStore) : IRequestHandler<GetServicesFromProviderQuery, Result<List<ServiceEntity>>>
{
    public async Task<Result<List<ServiceEntity>>> Handle(GetServicesFromProviderQuery request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        await mediator.Publish(new GetServicesFromProviderEvent { Email = request.Email }, cancellationToken);

        var filter = SupportTools<ProviderEntity>.FilterByEmail(request.Email);
        var providerEntity = await providerService.FindProvidersAsync(filter);
        if (providerEntity is not null)
        {
            // Counts the services disclosed, not the single provider they were read from.
            await eventStore.SaveAsync(QueryAudit.Success(nameof(GetServicesFromProviderQuery), providerEntity.ServiceEntities.Count));
            return Result.Ok(providerEntity.ServiceEntities);
        }

        await eventStore.SaveAsync(QueryAudit.Failure(nameof(GetServicesFromProviderQuery)));
        return Result.Ok(new List<ServiceEntity>());
    }
}
