namespace Customer.Tests.Commands;

public class AddCustomerCommandHandlerTest
{
    private static CustomerEntity Customer(string firstName = "Ada", string lastName = "Lovelace", string email = "customer@example.com") => new()
    {
        FirstName = firstName,
        LastName = lastName,
        Email = email
    };

    [Fact]
    public async Task Handle_NoDuplicateAndKafkaSucceeds_PersistsAndReturnsOk()
    {
        var customer = Customer();
        var customerService = new Mock<ICustomerService>();
        customerService.Setup(c => c.FindCustomerAsync(It.IsAny<BsonDocument>())).ReturnsAsync((CustomerEntity)null!);
        var kafkaClient = new Mock<IKafkaClient>();
        kafkaClient.Setup(k => k.CreateTopicIfNotExist(It.IsAny<string>())).ReturnsAsync("created");
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new AddCustomerCommandHandler(mediator.Object, kafkaClient.Object, customerService.Object, eventStore.Object);

        var result = await handler.Handle(new AddCustomerCommand { CustomerEntity = customer }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        // The persisted KafkaTopic is the DETERMINISTIC topic name (KafkaHelper.CreateCustomerTopicName),
        // not the client's return value -- that value is only inspected for the "exception" prefix.
        Assert.Equal(AgendaBuddy.Kafka.Support.KafkaHelper.CreateCustomerTopicName(customer.Email), result.Value.KafkaTopic);
        customerService.Verify(c => c.AddCustomerAsync(customer), Times.Once);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "AddCustomerCommand")), Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateNameFound_ReturnsFailWithNoKafkaCallAndNoAuditWrite()
    {
        // Preserves Customer/Program.cs's pre-existing order: the duplicate check runs BEFORE any Kafka
        // call, mediator.Publish, or event store write -- a duplicate never touched any of the three.
        var customer = Customer();
        var customerService = new Mock<ICustomerService>();
        customerService.Setup(c => c.FindCustomerAsync(It.IsAny<BsonDocument>())).ReturnsAsync(Customer());
        var kafkaClient = new Mock<IKafkaClient>();
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new AddCustomerCommandHandler(mediator.Object, kafkaClient.Object, customerService.Object, eventStore.Object);

        var result = await handler.Handle(new AddCustomerCommand { CustomerEntity = customer }, CancellationToken.None);

        Assert.True(result.IsFailed);
        kafkaClient.Verify(k => k.CreateTopicIfNotExist(It.IsAny<string>()), Times.Never);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Never);
        eventStore.Verify(e => e.SaveAsync(It.IsAny<Event>()), Times.Never);
    }

    [Fact]
    public async Task Handle_KafkaReportsAnException_ReturnsFailAndWritesFailedAuditWithTheExceptionType()
    {
        var customer = Customer();
        var customerService = new Mock<ICustomerService>();
        customerService.Setup(c => c.FindCustomerAsync(It.IsAny<BsonDocument>())).ReturnsAsync((CustomerEntity)null!);
        var kafkaClient = new Mock<IKafkaClient>();
        kafkaClient.Setup(k => k.CreateTopicIfNotExist(It.IsAny<string>())).ReturnsAsync("Exception: broker unreachable");
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new AddCustomerCommandHandler(mediator.Object, kafkaClient.Object, customerService.Object, eventStore.Object);

        var result = await handler.Handle(new AddCustomerCommand { CustomerEntity = customer }, CancellationToken.None);

        Assert.True(result.IsFailed);
        customerService.Verify(c => c.AddCustomerAsync(It.IsAny<CustomerEntity>()), Times.Never);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev =>
            ev.Status == "Failed" && ev.Type.StartsWith("AddCustomerCommand - Exception"))), Times.Once);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new AddCustomerCommandHandler(
            Mock.Of<IMediator>(), Mock.Of<IKafkaClient>(), Mock.Of<ICustomerService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
