namespace AgendaBuddy.Provider.Core.Showcase;

/// <summary>
/// Sets or clears the photo or the logo. A hash that is not one of the caller's own uploads is refused, never
/// stored: the media route would answer 404 for it and the screen would show an empty frame behind a 200.
/// </summary>
public class SetShowcaseImageCommandHandler(IShowcaseService showcaseService, IEventStore eventStore)
    : IRequestHandler<SetShowcaseImageCommand, Result<ShowcaseOwnerView>>
{
    public async Task<Result<ShowcaseOwnerView>> Handle(SetShowcaseImageCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var provider = await showcaseService.FindProviderByEmailAsync(request.Email);
        if (provider is null)
            return await FailAsync(request, ShowcaseError.NotFound());

        if (request.Hash is not null && !ShowcaseRules.IsHash(request.Hash))
            return await FailAsync(request, ShowcaseError.UnknownMedia());

        var field = request.Slot == ShowcaseImageSlot.Photo ? ShowcaseService.PhotoField : ShowcaseService.LogoField;
        var result = await showcaseService.SetImageAsync(provider, field, request.Hash);
        if (result.Status != ShowcaseWriteStatus.Ok || result.Showcase is null)
            return await FailAsync(request, ShowcaseError.UnknownMedia());

        await eventStore.SaveAsync(ShowcaseProjection.Audit(nameof(SetShowcaseImageCommand), "Success", request));
        return Result.Ok(await ShowcaseProjection.OwnerViewAsync(showcaseService, provider, result.Showcase));
    }

    private async Task<Result<ShowcaseOwnerView>> FailAsync(SetShowcaseImageCommand request, ShowcaseError error)
    {
        await eventStore.SaveAsync(ShowcaseProjection.Audit(nameof(SetShowcaseImageCommand), "Failed", request));
        return Result.Fail(error);
    }
}
