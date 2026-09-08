namespace AgendaBuddy.Provider.Core.Commands;

/// <summary>
/// Sets a provider's avatar to one of the built-in marks.
/// </summary>
/// <remarks>
/// <b>The id is checked against the catalogue this build ships, and an unknown one is refused.</b> Storing it
/// unvalidated would not throw anywhere — <c>AvatarCatalog.Resolve</c> treats an unknown id as absent and quietly
/// falls back to the email-derived mark — so the account would keep showing its old avatar with a successful save
/// behind it, which is indistinguishable from the feature not working.
/// </remarks>
public class SetProviderAvatarCommandHandler(
    IProviderService providerService,
    IEventStore eventStore)
    : IRequestHandler<SetProviderAvatarCommand, Result<ProviderEntity>>
{
    public async Task<Result<ProviderEntity>> Handle(
        SetProviderAvatarCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        if (!AvatarCatalog.IsKnown(request.AvatarId))
        {
            await SaveAsync("Failed", request);
            return Result.Fail<ProviderEntity>($"'{request.AvatarId}' is not an avatar this build offers.");
        }

        var updated = await providerService.SetAvatarAsync(request.Email, request.AvatarId);
        if (updated is null)
        {
            await SaveAsync("Failed", request);
            return Result.Fail<ProviderEntity>($"No provider found with email {request.Email}");
        }

        await SaveAsync("Success", request);
        return Result.Ok(updated);
    }

    private Task SaveAsync(string status, SetProviderAvatarCommand request) =>
        eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = status,
            Type = nameof(SetProviderAvatarCommand),
            Data = JsonSerializer.Serialize(request)
        });
}
