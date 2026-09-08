namespace AgendaBuddy.Provider.Core.Commands;

/// <summary>
/// Records a provider's acceptance of the Terms and Conditions and the Privacy Policy.
/// </summary>
/// <remarks>
/// <b>The timestamp is the server's, not the client's.</b> A consent record whose date the consenting party
/// supplies proves nothing, and a device with a wrong clock would file the acceptance in the wrong year.
/// </remarks>
public class SetProviderConsentCommandHandler(
    IProviderService providerService,
    IEventStore eventStore)
    : IRequestHandler<SetProviderConsentCommand, Result<ProviderEntity>>
{
    public async Task<Result<ProviderEntity>> Handle(
        SetProviderConsentCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var now = DateTime.UtcNow;

        var updated = await providerService.SetConsentAsync(
            request.Email,
            request.AcceptedTerms ? now : null,
            request.AcceptedPrivacy ? now : null);

        if (updated is null)
        {
            await SaveAsync("Failed", request);
            return Result.Fail<ProviderEntity>($"No provider found with email {request.Email}");
        }

        await SaveAsync("Success", request);
        return Result.Ok(updated);
    }

    private Task SaveAsync(string status, SetProviderConsentCommand request) =>
        eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = status,
            Type = nameof(SetProviderConsentCommand),
            Data = JsonSerializer.Serialize(request)
        });
}
