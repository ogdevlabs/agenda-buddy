namespace AgendaBuddy.Provider.Core.Showcase;

public class GetMyShowcaseQueryHandler(IShowcaseService showcaseService, IEventStore eventStore)
    : IRequestHandler<GetMyShowcaseQuery, Result<ShowcaseOwnerView>>
{
    public async Task<Result<ShowcaseOwnerView>> Handle(GetMyShowcaseQuery request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var provider = await showcaseService.FindProviderByEmailAsync(request.Email);
        if (provider is null)
        {
            await eventStore.SaveAsync(QueryAudit.Failure(nameof(GetMyShowcaseQuery)));
            return Result.Fail(ShowcaseError.NotFound());
        }

        var showcase = await showcaseService.EnsureShowcaseAsync(provider);
        await eventStore.SaveAsync(QueryAudit.Success(nameof(GetMyShowcaseQuery), 1));
        return Result.Ok(await ShowcaseProjection.OwnerViewAsync(showcaseService, provider, showcase));
    }
}
