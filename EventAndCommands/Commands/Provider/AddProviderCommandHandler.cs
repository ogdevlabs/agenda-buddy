namespace EventAndCommands.Commands.Provider;

[RegisterService(ServiceLifetime.Scoped)]
public class AddProviderCommandHandler(
    IMediator mediator,
    ProviderService providerService,
    ProviderEntity providerEntity)
    : IRequestHandler<AddProviderCommand, string>
{
    [InjectService] private IEventStore EventStore { get; } = new EventStore();

    private string TopicName { get; set; } = string.Empty;

    public async Task<string> Handle(AddProviderCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new AddProviderEvent(), cancellationToken);
        TopicName = providerEntity.KafkaTopic!;
        await providerService.AddProviderAsync(providerEntity);
        var successEvent = new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Success",
            Type = "AddProviderCommand",
            Data = JsonSerializer.Serialize(providerEntity)
        };
        await EventStore.SaveAsync(successEvent);
        return await Task.FromResult(TopicName);
    }
}