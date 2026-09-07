namespace AgendaBuddy.Provider.Core.Commands;

// The duplicate check lives here, not in AgendaBuddy.Provider.Api, so the Api project stays
// endpoint/DI wiring only, per the architecture doc.
public class AddProviderCommandHandler(
    IMediator mediator,
    IProviderService providerService,
    IEventStore eventStore)
    : IRequestHandler<AddProviderCommand, Result<ProviderEntity>>
{
    public async Task<Result<ProviderEntity>> Handle(AddProviderCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var providerEntity = request.ProviderEntity;

        // The email is the identity — it is what OwnershipGuard authorises off, what a message thread is
        // keyed on and what a subscription names. A person may hold several addresses and any number of
        // people share a name, so matching on first+last name locked every second "John Smith" out of ever
        // having a profile, and reported it as a conflict on an email address that was not in use.
        // Runs before anything is persisted or published.
        var existingProvider = await providerService.FindProvidersAsync(
            SupportTools<ProviderEntity>.FilterByEmail(providerEntity.Email));
        if (existingProvider is not null)
            return Result.Fail<ProviderEntity>($"Existing record found for Email:{providerEntity.Email}");

        // Assigned once, at creation, and only when the caller supplied nothing — a later update must not
        // reshuffle somebody's avatar, and a client that does send one has chosen it deliberately.
        if (!AvatarCatalog.IsKnown(providerEntity.AvatarId))
            providerEntity.AvatarId = AvatarCatalog.Random();

        await mediator.Publish(new AddProviderEvent { ProviderName = providerEntity.Email }, cancellationToken);

        await providerService.AddProviderAsync(providerEntity);
        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Success",
            Type = nameof(AddProviderCommand),
            Data = JsonSerializer.Serialize(providerEntity)
        });
        return Result.Ok(providerEntity);
    }
}
