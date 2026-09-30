namespace AgendaBuddy.Provider.Core.Showcase;

public class UpdatePortfolioItemCommandHandler(IShowcaseService showcaseService, IEventStore eventStore)
    : IRequestHandler<UpdatePortfolioItemCommand, Result<PortfolioItemView>>
{
    public async Task<Result<PortfolioItemView>> Handle(UpdatePortfolioItemCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var caption = ShowcaseRules.Clean(request.Caption);
        if (!ShowcaseRules.FitsWithin(caption, ShowcaseRules.MaxCaption))
            return await FailAsync(request, ShowcaseError.Invalid("caption", "too-long",
                $"Keep captions to {ShowcaseRules.MaxCaption} characters."));
        if (!ShowcaseProjection.IsWellFormedServiceId(request.ServiceId))
            return await FailAsync(request, ShowcaseError.InvalidService());

        var provider = ShowcaseRules.IsHash(request.Hash)
            ? await showcaseService.FindProviderByEmailAsync(request.Email)
            : null;
        if (provider is null)
            return await FailAsync(request, ShowcaseError.NotFound());

        var result = await showcaseService.UpdatePortfolioItemAsync(provider, request.Hash, caption, request.ServiceId);
        if (result.Status == ShowcaseWriteStatus.InvalidService)
            return await FailAsync(request, ShowcaseError.InvalidService());
        if (result.Status != ShowcaseWriteStatus.Ok || result.Item is null)
            return await FailAsync(request, ShowcaseError.NotFound());

        await eventStore.SaveAsync(ShowcaseProjection.Audit(nameof(UpdatePortfolioItemCommand), "Success", request));
        return Result.Ok(PortfolioItemView.From(result.Item));
    }

    private async Task<Result<PortfolioItemView>> FailAsync(UpdatePortfolioItemCommand request, ShowcaseError error)
    {
        await eventStore.SaveAsync(ShowcaseProjection.Audit(nameof(UpdatePortfolioItemCommand), "Failed", request));
        return Result.Fail(error);
    }
}
