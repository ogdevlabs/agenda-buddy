namespace AgendaBuddy.Provider.Core.Showcase;

/// <summary>Both fields are always written: <c>null</c> clears, so an omitted field cannot leave stale text.</summary>
public class SetShowcaseTextCommandHandler(IShowcaseService showcaseService, IEventStore eventStore)
    : IRequestHandler<SetShowcaseTextCommand, Result<ShowcaseOwnerView>>
{
    public async Task<Result<ShowcaseOwnerView>> Handle(SetShowcaseTextCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var tagline = ShowcaseRules.Clean(request.Tagline);
        var about = ShowcaseRules.Clean(request.About);

        ShowcaseError? invalid = null;
        if (!ShowcaseRules.FitsWithin(tagline, ShowcaseRules.MaxTagline))
            invalid = ShowcaseError.Invalid("tagline", "too-long",
                $"Keep your tagline to {ShowcaseRules.MaxTagline} characters.");
        else if (!ShowcaseRules.FitsWithin(about, ShowcaseRules.MaxAbout))
            invalid = ShowcaseError.Invalid("about", "too-long",
                $"Keep About to {ShowcaseRules.MaxAbout} characters.");

        var provider = invalid is null ? await showcaseService.FindProviderByEmailAsync(request.Email) : null;
        if (invalid is not null || provider is null)
        {
            await eventStore.SaveAsync(ShowcaseProjection.Audit(nameof(SetShowcaseTextCommand), "Failed", request));
            return Result.Fail(invalid ?? ShowcaseError.NotFound());
        }

        var showcase = await showcaseService.SetTextAsync(provider, tagline, about);
        await eventStore.SaveAsync(ShowcaseProjection.Audit(nameof(SetShowcaseTextCommand), "Success", request));
        return Result.Ok(await ShowcaseProjection.OwnerViewAsync(showcaseService, provider, showcase));
    }
}
