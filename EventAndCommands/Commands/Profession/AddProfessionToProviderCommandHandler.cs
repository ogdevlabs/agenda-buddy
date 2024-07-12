namespace EventAndCommands.Commands.Profession;

public class AddProfessionToProviderCommandHandler(
    IMediator mediator,
    ProviderService providerService,
    string email,
    List<ProfessionEntity> professionEntities) : IRequestHandler<AddProfessionsToProviderCommand, ProviderEntity>
{
    [InjectService] private IEventStore? EventStore { get; } = new EventStore();

    public async Task<ProviderEntity> Handle(AddProfessionsToProviderCommand request,
        CancellationToken cancellationToken)
    {
        SanitizeProfessionList();
        if (professionEntities.Count == 0) return null!;

        await mediator.Publish(new AddProfessionsToProviderEvent()
        {
            ProfessionEntities = professionEntities
        }, cancellationToken);

        var provider = await providerService.FindProvidersAsync(SupportTools<ProviderEntity>.FilterByEmail(email));
        if (provider != null)
        {
            provider.ProfessionCollection.AddRange(
                SupportTools<ProviderEntity>.GenerateIdForRecord(professionEntities));
            var updateResult = await providerService.UpdateProviderAsync(provider.Id.ToString(), provider);
            if (updateResult)
            {
                var successEvent = new Event
                {
                    Id = ObjectId.GenerateNewId(),
                    TimeStamp = DateTime.UtcNow,
                    Status = "Success",
                    Type = "AddProfessionsToProviderCommand",
                    Data = JsonSerializer.Serialize(provider)
                };
                await EventStore!.SaveAsync(successEvent);
                return await Task.FromResult(provider);
            }
        }
        else
        {
            var failEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = "AddProfessionsToProviderCommand",
                Data = JsonSerializer.Serialize(new ProviderEntity())
            };
            await EventStore!.SaveAsync(failEvent);
        }

        return null!;
    }

    private void SanitizeProfessionList()
    {
        professionEntities = ProfessionHelper.CleanUpDefaultProfessionEntities(professionEntities);
        professionEntities = ProfessionHelper.RemoveDuplicateProfessionEntities(professionEntities);
    }
}