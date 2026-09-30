namespace AgendaBuddy.Provider.Core.Showcase;

/// <summary>
/// A showcase as a signed-in viewer sees it. Unknown, deactivated, erased, taken-down and hidden-by-you are one
/// 404, so the route cannot be used to tell them apart. A provider without a showcase document still answers
/// with the empty state. The owner viewing their own (Preview) records no visit.
/// </summary>
public class GetShowcaseQueryHandler(IShowcaseService showcaseService, IEventStore eventStore)
    : IRequestHandler<GetShowcaseQuery, Result<ShowcaseView>>
{
    public async Task<Result<ShowcaseView>> Handle(GetShowcaseQuery request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        ProviderShowcaseEntity? showcase = null;
        ProviderEntity? provider;
        if (request.Code is not null)
        {
            showcase = await showcaseService.FindShowcaseByCodeAsync(request.Code);
            provider = showcase is null ? null : await showcaseService.FindProviderByRefAsync(showcase.ProviderId.ToString());
        }
        else
        {
            provider = await showcaseService.FindProviderByRefAsync(request.ProviderRef);
            if (provider is not null)
                showcase = await showcaseService.FindShowcaseAsync(provider.Id);
        }

        if (provider is null || !await showcaseService.IsVisibleToAsync(provider, showcase, request.CallerEmail))
        {
            await eventStore.SaveAsync(QueryAudit.Failure(nameof(GetShowcaseQuery)));
            return Result.Fail(ShowcaseError.NotFound());
        }

        var relationship = await showcaseService.GetRelationshipAsync(provider, request.CallerEmail);
        DateTime? previousVisit = null;
        if (!relationship.IsSelf)
        {
            var source = ShowcaseSources.Normalise(request.Source ?? (request.Code is null ? null : ShowcaseSources.Code));
            previousVisit = await showcaseService.RecordVisitAsync(provider.Id, request.CallerEmail, source);
        }

        var portfolio = (showcase?.Portfolio ?? [])
            .Select(i => new PublicPortfolioItemView(i.Hash, i.Caption, i.ServiceId?.ToString(), i.Width, i.Height,
                previousVisit is not null && i.AddedAt > previousVisit))
            .ToList();

        var next = relationship.NextAppointment;
        var view = new ShowcaseView(
            provider.Id.ToString(),
            provider.FirstName,
            provider.LastName,
            provider.Professions,
            showcase?.Tagline,
            showcase?.About,
            showcase?.PhotoHash,
            showcase?.LogoHash,
            AvatarCatalog.Resolve(provider.AvatarId, provider.Email),
            portfolio,
            ShowcaseProjection.BookableServices(provider),
            new RelationshipView(
                relationship.IsSelf,
                relationship.IsSubscribed,
                next is null ? null : new NextAppointmentView(next.Identifier, next.Start, next.ServiceName),
                relationship.HasBookedBefore));

        await eventStore.SaveAsync(QueryAudit.Success(nameof(GetShowcaseQuery), 1));
        return Result.Ok(view);
    }
}
