namespace AgendaBuddy.Provider.Core.Showcase;

/// <summary>
/// The list must be a permutation of the current portfolio. Anything else — typically an edit made on another
/// device — is a 409 the client resolves by reloading, never a partial reorder.
/// </summary>
public class ReorderPortfolioCommandHandler(IShowcaseService showcaseService, IEventStore eventStore)
    : IRequestHandler<ReorderPortfolioCommand, Result<ShowcaseOwnerView>>
{
    public async Task<Result<ShowcaseOwnerView>> Handle(ReorderPortfolioCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var provider = await showcaseService.FindProviderByEmailAsync(request.Email);
        if (provider is null)
            return await FailAsync(request, ShowcaseError.NotFound());

        var result = await showcaseService.ReorderPortfolioAsync(provider, request.Hashes);
        if (result.Status != ShowcaseWriteStatus.Ok || result.Showcase is null)
            return await FailAsync(request, ShowcaseError.Conflict("portfolio-changed",
                "Your portfolio changed on another device. We've reloaded it — try again."));

        await eventStore.SaveAsync(ShowcaseProjection.Audit(nameof(ReorderPortfolioCommand), "Success", request));
        return Result.Ok(await ShowcaseProjection.OwnerViewAsync(showcaseService, provider, result.Showcase));
    }

    private async Task<Result<ShowcaseOwnerView>> FailAsync(ReorderPortfolioCommand request, ShowcaseError error)
    {
        await eventStore.SaveAsync(ShowcaseProjection.Audit(nameof(ReorderPortfolioCommand), "Failed", request));
        return Result.Fail(error);
    }
}
