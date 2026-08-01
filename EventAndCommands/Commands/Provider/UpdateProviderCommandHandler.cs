namespace EventAndCommands.Commands.Provider;

public class UpdateProviderCommandHandler(
    string email,
    IMediator mediator,
    ProviderService providerService,
    ProviderEntity providerEntity,
    IEventStore eventStore)
    : IRequestHandler<UpdateProviderCommand, string>
{

    public async Task<string> Handle(UpdateProviderCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new UpdateProviderEvent
        {
            ProviderEntity = request.ProviderEntity
        }, cancellationToken);
        var record = await providerService
            .FindProvidersAsync(SupportTools<ProviderEntity>.FilterByEmail(email));
        if (record != null)
        {
            providerEntity.Id = record.Id;
            var updateResult = await providerService.UpdateProviderAsync(record.Id.ToString(), providerEntity);
            if (updateResult)
            {
                var successEvent = new Event
                {
                    Id = ObjectId.GenerateNewId(),
                    TimeStamp = DateTime.UtcNow,
                    Status = "Success",
                    Type = "UpdateProviderCommand",
                    Data = JsonSerializer.Serialize(providerEntity)
                };
                await eventStore.SaveAsync(successEvent);
                return await Task.FromResult(providerEntity.ToJson());
            }
        }
        else
        {
            var failEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = "UpdateProviderCommand",
                Data = JsonSerializer.Serialize(providerEntity)
            };
            await eventStore.SaveAsync(failEvent);
        }

        return await Task.FromResult(string.Empty);
    }
}