namespace AgendaBuddy.Provider.Core.Showcase;

/// <summary>
/// Resolves photos for screens keyed on email. Only the caller's own counterparties resolve; any other address is
/// silently omitted, so the route cannot turn an address into a provider reference.
/// </summary>
public class LookupShowcaseAvatarsQueryHandler(IShowcaseService showcaseService, IEventStore eventStore)
    : IRequestHandler<LookupShowcaseAvatarsQuery, Result<IReadOnlyList<ShowcaseAvatarLookup>>>
{
    public async Task<Result<IReadOnlyList<ShowcaseAvatarLookup>>> Handle(
        LookupShowcaseAvatarsQuery request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        if (request.Emails.Count > ShowcaseRules.MaxLookupEmails)
        {
            await eventStore.SaveAsync(QueryAudit.Failure(nameof(LookupShowcaseAvatarsQuery)));
            return Result.Fail(ShowcaseError.Invalid("emails", "too-many",
                $"Ask for at most {ShowcaseRules.MaxLookupEmails} addresses at a time."));
        }

        var found = await showcaseService.LookupAsync(request.CallerEmail, request.CallerIsProvider, request.Emails);
        await eventStore.SaveAsync(QueryAudit.Success(nameof(LookupShowcaseAvatarsQuery), found.Count));
        return Result.Ok(found);
    }
}
