namespace AgendaBuddy.Profession.Core.Queries;

// A missing provider is a successful EMPTY read here, matching Services' own
// GetServicesFromProviderQueryHandler -- a caller checking their own profile before it exists should
// see an empty list, not a 404.
public class GetProfessionsFromProviderQueryHandler(
    IMediator mediator,
    IProviderService providerService,
    IEventStore eventStore) : IRequestHandler<GetProfessionsFromProviderQuery, Result<List<string>>>
{
    public async Task<Result<List<string>>> Handle(GetProfessionsFromProviderQuery request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        await mediator.Publish(new GetProfessionsFromProviderEvent { Email = request.Email }, cancellationToken);

        var filter = SupportTools<ProviderEntity>.FilterByEmail(request.Email);
        var providerEntity = await providerService.FindProvidersAsync(filter);
        if (providerEntity is not null)
        {
            await eventStore.SaveAsync(QueryAudit.Success(nameof(GetProfessionsFromProviderQuery), providerEntity.Professions.Count));
            return Result.Ok(providerEntity.Professions);
        }

        await eventStore.SaveAsync(QueryAudit.Failure(nameof(GetProfessionsFromProviderQuery)));
        return Result.Ok(new List<string>());
    }
}
