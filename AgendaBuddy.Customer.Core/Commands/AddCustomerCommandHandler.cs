namespace AgendaBuddy.Customer.Core.Commands;

// The duplicate-email check and the Kafka topic creation live here so AgendaBuddy.Customer.Api stays
// endpoint/DI wiring only, per the architecture doc.
//
// IKafkaClient stays interface-typed, not the concrete KafkaClient class (agenda-buddy-5og). A prior
// handler's constructor was typed to the concrete KafkaClient, only safe because it hand-cast
// `(kafkaClient as KafkaClient)!` from the IKafkaClient DI registration -- fixed here as a natural
// consequence of moving to real mediator.Send dispatch.
public class AddCustomerCommandHandler(
    IMediator mediator,
    IKafkaClient kafkaClient,
    ICustomerService customerService,
    IEventStore eventStore)
    : IRequestHandler<AddCustomerCommand, Result<CustomerEntity>>
{
    public async Task<Result<CustomerEntity>> Handle(AddCustomerCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var customerEntity = request.CustomerEntity;

        // Preserved exactly from Customer/Program.cs: matches by NAME, not by email, and this check runs
        // BEFORE any Kafka call -- a duplicate never touches the broker, mediator.Publish, or the event
        // store, matching the pre-refactor route's own order.
        var existingCustomer = await customerService.FindCustomerAsync(
            SupportTools<CustomerEntity>.FilterByNameAndLastName(customerEntity.FirstName!, customerEntity.LastName!));
        if (existingCustomer is not null)
            return Result.Fail<CustomerEntity>($"Existing record found for Email:{customerEntity.Email}");

        var topicName = KafkaHelper.CreateCustomerTopicName(customerEntity.Email!);
        var kafkaTopic = await kafkaClient.CreateTopicIfNotExist(topicName);
        await mediator.Publish(new AddCustomerEvent { CustomerEntity = customerEntity }, cancellationToken);

        if (!string.IsNullOrEmpty(kafkaTopic) && !kafkaTopic.ToLower().StartsWith("exception"))
        {
            customerEntity.KafkaTopic = topicName;
            await customerService.AddCustomerAsync(customerEntity);
            await eventStore.SaveAsync(new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = nameof(AddCustomerCommand),
                Data = JsonSerializer.Serialize(customerEntity)
            });
            return Result.Ok(customerEntity);
        }

        // CustomerAuditTest.AC7_ACreateWithNoKafkaBrokerReachable_WritesAFailedAuditEvent pins the exact
        // "AddCustomerCommand - Exception..." shape this Type carries.
        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Failed",
            Type = $"{nameof(AddCustomerCommand)} - {kafkaTopic}",
            Data = JsonSerializer.Serialize(customerEntity)
        });
        return Result.Fail<CustomerEntity>(kafkaTopic);
    }
}
