namespace AgendaBuddy.Provider.Core.Commands;

// F-020-T11: moved from AgendaBuddy.EventAndCommands.Commands.Provider, following Booking's/Calendar's/
// Profession's/Services' precedent. The duplicate-name check and the Kafka topic creation both used to
// live in Provider/Program.cs (deleted) -- moved here so AgendaBuddy.Provider.Api stays endpoint/DI
// wiring only, per the architecture doc. IKafkaClient stays interface-typed, not the concrete
// KafkaClient class (agenda-buddy-5og, F-018) -- confirmed unchanged by this move.
public class AddProviderCommandHandler(
    IMediator mediator,
    IKafkaClient kafkaClient,
    IProviderService providerService,
    IEventStore eventStore)
    : IRequestHandler<AddProviderCommand, Result<ProviderEntity>>
{
    public async Task<Result<ProviderEntity>> Handle(AddProviderCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var providerEntity = request.ProviderEntity;

        // Preserved exactly from Provider/Program.cs: matches by NAME, not by email, and this check runs
        // BEFORE any Kafka call -- a duplicate never touches the broker, mediator.Publish, or the event
        // store, matching the pre-refactor route's own order (it never called into RequestCollection at
        // all for this branch).
        var existingProvider = await providerService.FindProvidersAsync(
            SupportTools<ProviderEntity>.FilterByNameAndLastName(providerEntity.FirstName, providerEntity.LastName));
        if (existingProvider is not null)
            return Result.Fail<ProviderEntity>($"Existing record found for Email:{providerEntity.Email}");

        var topicName = KafkaHelper.CreateProviderTopicName(providerEntity.Email!);
        var kafkaTopic = await kafkaClient.CreateTopicIfNotExist(topicName);
        await mediator.Publish(new AddProviderEvent { ProviderName = topicName }, cancellationToken);

        if (!string.IsNullOrEmpty(kafkaTopic) && !kafkaTopic.ToLower().StartsWith("exception"))
        {
            providerEntity.KafkaTopic = topicName;
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

        // ProviderAuditTest.AC7_ACreateWithNoKafkaBrokerReachable_WritesAFailedAuditEvent pins the exact
        // "AddProviderCommand - Exception..." shape this Type carries.
        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Failed",
            Type = $"{nameof(AddProviderCommand)} - {kafkaTopic}",
            Data = JsonSerializer.Serialize(providerEntity)
        });
        return Result.Fail<ProviderEntity>(kafkaTopic);
    }
}
