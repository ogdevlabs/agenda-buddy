namespace AgendaBuddy.Provider.Core.Showcase;

/// <summary>
/// Adds one image. Re-adding a hash already present succeeds without a second copy, which is what makes a retried
/// upload and an undone removal both safe.
/// </summary>
public class AddPortfolioItemCommandHandler(IShowcaseService showcaseService, IEventStore eventStore)
    : IRequestHandler<AddPortfolioItemCommand, Result<PortfolioWrite>>
{
    public async Task<Result<PortfolioWrite>> Handle(AddPortfolioItemCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var caption = ShowcaseRules.Clean(request.Caption);
        if (!ShowcaseRules.IsHash(request.Hash))
            return await FailAsync(request, ShowcaseError.UnknownMedia());
        if (!ShowcaseRules.FitsWithin(caption, ShowcaseRules.MaxCaption))
            return await FailAsync(request, ShowcaseError.Invalid("caption", "too-long",
                $"Keep captions to {ShowcaseRules.MaxCaption} characters."));
        if (!ShowcaseProjection.IsWellFormedServiceId(request.ServiceId))
            return await FailAsync(request, ShowcaseError.InvalidService());

        var provider = await showcaseService.FindProviderByEmailAsync(request.Email);
        if (provider is null)
            return await FailAsync(request, ShowcaseError.NotFound());

        var result = await showcaseService.AddPortfolioItemAsync(provider, request.Hash, caption, request.ServiceId);
        switch (result.Status)
        {
            case ShowcaseWriteStatus.Ok or ShowcaseWriteStatus.AlreadyPresent when result.Item is not null:
                await eventStore.SaveAsync(ShowcaseProjection.Audit(nameof(AddPortfolioItemCommand), "Success", request));
                return Result.Ok(new PortfolioWrite(
                    PortfolioItemView.From(result.Item), result.Status == ShowcaseWriteStatus.Ok));
            case ShowcaseWriteStatus.InvalidService:
                return await FailAsync(request, ShowcaseError.InvalidService());
            case ShowcaseWriteStatus.PortfolioFull:
                return await FailAsync(request, ShowcaseError.Conflict("portfolio-full",
                    $"Your portfolio already has {ShowcaseRules.MaxPortfolioItems} images. Remove one to add another."));
            default:
                return await FailAsync(request, ShowcaseError.UnknownMedia());
        }
    }

    private async Task<Result<PortfolioWrite>> FailAsync(AddPortfolioItemCommand request, ShowcaseError error)
    {
        await eventStore.SaveAsync(ShowcaseProjection.Audit(nameof(AddPortfolioItemCommand), "Failed", request));
        return Result.Fail(error);
    }
}
