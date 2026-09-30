namespace AgendaBuddy.Provider.Core.Showcase;

/// <summary>Idempotent in both directions. Hiding an unknown provider is a 404, the same as viewing one.</summary>
public class SetShowcaseHiddenCommandHandler(IShowcaseService showcaseService, IEventStore eventStore)
    : IRequestHandler<SetShowcaseHiddenCommand, Result>
{
    public async Task<Result> Handle(SetShowcaseHiddenCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var provider = await showcaseService.FindProviderByRefAsync(request.ProviderRef);
        if (provider is null)
        {
            await eventStore.SaveAsync(ShowcaseProjection.Audit(nameof(SetShowcaseHiddenCommand), "Failed", request));
            return Result.Fail(ShowcaseError.NotFound());
        }

        if (request.Hidden)
            await showcaseService.BlockAsync(request.CustomerEmail, provider.Id);
        else
            await showcaseService.UnblockAsync(request.CustomerEmail, provider.Id);

        await eventStore.SaveAsync(ShowcaseProjection.Audit(nameof(SetShowcaseHiddenCommand), "Success", request));
        return Result.Ok();
    }
}
