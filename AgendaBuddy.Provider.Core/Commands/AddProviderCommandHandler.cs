namespace AgendaBuddy.Provider.Core.Commands;

// The duplicate-name check lives here, not in AgendaBuddy.Provider.Api, so the Api project stays
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

        // Matches by NAME, not by email, and runs before anything is persisted or published.
        var existingProvider = await providerService.FindProvidersAsync(
            SupportTools<ProviderEntity>.FilterByNameAndLastName(providerEntity.FirstName, providerEntity.LastName));
        if (existingProvider is not null)
            return Result.Fail<ProviderEntity>($"Existing record found for Email:{providerEntity.Email}");

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
