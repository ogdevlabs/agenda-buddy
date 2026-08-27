namespace AgendaBuddy.Provider.Core.Queries;

// F-020-T11: moved from AgendaBuddy.EventAndCommands.Queries.Provider. Constructor takes only
// DI-resolvable services -- the pre-refactor handler took `email` as a per-instance constructor
// parameter; it now comes from the query.
public class GetProviderByEmailQueryHandler(
    IMediator mediator,
    IProviderService providerService,
    IEventStore eventStore)
    : IRequestHandler<GetProviderByEmailQuery, Result<ProviderEntity>>
{
    public async Task<Result<ProviderEntity>> Handle(GetProviderByEmailQuery request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        await mediator.Publish(new GetProviderByEmailEvent { Email = request.Email }, cancellationToken);

        var filter = SupportTools<ProviderEntity>.FilterByEmail(request.Email);
        var providerEntity = await providerService.FindProvidersAsync(filter);
        if (providerEntity is not null)
        {
            await eventStore.SaveAsync(QueryAudit.Success(nameof(GetProviderByEmailQuery), 1));
            return Result.Ok(providerEntity);
        }

        await eventStore.SaveAsync(QueryAudit.Failure(nameof(GetProviderByEmailQuery)));
        return Result.Fail<ProviderEntity>($"No provider found with email {request.Email}");
    }
}
