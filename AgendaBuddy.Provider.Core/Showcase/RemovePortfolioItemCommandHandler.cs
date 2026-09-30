namespace AgendaBuddy.Provider.Core.Showcase;

/// <summary>Idempotent: removing an image that is not there succeeds, so a retried delete is never an error.</summary>
public class RemovePortfolioItemCommandHandler(IShowcaseService showcaseService, IEventStore eventStore)
    : IRequestHandler<RemovePortfolioItemCommand, Result>
{
    public async Task<Result> Handle(RemovePortfolioItemCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var provider = ShowcaseRules.IsHash(request.Hash)
            ? await showcaseService.FindProviderByEmailAsync(request.Email)
            : null;
        if (provider is not null)
            await showcaseService.RemovePortfolioItemAsync(provider, request.Hash);

        await eventStore.SaveAsync(ShowcaseProjection.Audit(nameof(RemovePortfolioItemCommand),
            provider is null ? "Failed" : "Success", request));
        return Result.Ok();
    }
}
