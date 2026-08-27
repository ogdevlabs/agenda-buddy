namespace Provider.Tests.Commands;

public class AddProviderCommandHandlerTest
{
    private static ProviderEntity Provider(string firstName = "Grace", string lastName = "Hopper", string email = "provider@example.com") => new()
    {
        FirstName = firstName,
        LastName = lastName,
        Email = email
    };

    [Fact]
    public async Task Handle_NoDuplicateAndKafkaSucceeds_PersistsAndReturnsOk()
    {
        var provider = Provider();
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync((ProviderEntity)null!);
        var kafkaClient = new Mock<IKafkaClient>();
        kafkaClient.Setup(k => k.CreateTopicIfNotExist(It.IsAny<string>())).ReturnsAsync("created");
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new AddProviderCommandHandler(mediator.Object, kafkaClient.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(new AddProviderCommand { ProviderEntity = provider }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        // The persisted KafkaTopic is the DETERMINISTIC topic name (KafkaHelper.CreateProviderTopicName),
        // not the client's return value -- that value is only inspected for the "exception" prefix.
        Assert.Equal(AgendaBuddy.Kafka.Support.KafkaHelper.CreateProviderTopicName(provider.Email), result.Value.KafkaTopic);
        providerService.Verify(p => p.AddProviderAsync(provider), Times.Once);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "AddProviderCommand")), Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateNameFound_ReturnsFailWithNoKafkaCallAndNoAuditWrite()
    {
        // Preserves Provider/Program.cs's pre-existing order: the duplicate check runs BEFORE any Kafka
        // call, mediator.Publish, or event store write -- a duplicate never touched any of the three.
        var provider = Provider();
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(Provider());
        var kafkaClient = new Mock<IKafkaClient>();
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new AddProviderCommandHandler(mediator.Object, kafkaClient.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(new AddProviderCommand { ProviderEntity = provider }, CancellationToken.None);

        Assert.True(result.IsFailed);
        kafkaClient.Verify(k => k.CreateTopicIfNotExist(It.IsAny<string>()), Times.Never);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Never);
        eventStore.Verify(e => e.SaveAsync(It.IsAny<Event>()), Times.Never);
    }

    [Fact]
    public async Task Handle_KafkaReportsAnException_ReturnsFailAndWritesFailedAuditWithTheExceptionType()
    {
        var provider = Provider();
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync((ProviderEntity)null!);
        var kafkaClient = new Mock<IKafkaClient>();
        kafkaClient.Setup(k => k.CreateTopicIfNotExist(It.IsAny<string>())).ReturnsAsync("Exception: broker unreachable");
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new AddProviderCommandHandler(mediator.Object, kafkaClient.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(new AddProviderCommand { ProviderEntity = provider }, CancellationToken.None);

        Assert.True(result.IsFailed);
        providerService.Verify(p => p.AddProviderAsync(It.IsAny<ProviderEntity>()), Times.Never);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev =>
            ev.Status == "Failed" && ev.Type.StartsWith("AddProviderCommand - Exception"))), Times.Once);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new AddProviderCommandHandler(
            Mock.Of<IMediator>(), Mock.Of<IKafkaClient>(), Mock.Of<IProviderService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
