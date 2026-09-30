namespace AgendaBuddy.Provider.Core.Showcase;

public class GetHiddenProvidersQueryHandler(IShowcaseService showcaseService, IEventStore eventStore)
    : IRequestHandler<GetHiddenProvidersQuery, Result<IReadOnlyList<HiddenProvider>>>
{
    public async Task<Result<IReadOnlyList<HiddenProvider>>> Handle(
        GetHiddenProvidersQuery request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var hidden = await showcaseService.GetHiddenProvidersAsync(request.CustomerEmail);
        await eventStore.SaveAsync(QueryAudit.Success(nameof(GetHiddenProvidersQuery), hidden.Count));
        return Result.Ok(hidden);
    }
}
