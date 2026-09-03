namespace AgendaBuddy.Customer.Tests.Commands;

public class AddCustomerCommandHandlerTest
{
    private static CustomerEntity Customer(string firstName = "Ada", string lastName = "Lovelace", string email = "customer@example.com") => new()
    {
        FirstName = firstName,
        LastName = lastName,
        Email = email
    };

    [Fact]
    public async Task Handle_NoDuplicate_PersistsAndReturnsOk()
    {
        var customer = Customer();
        var customerService = new Mock<ICustomerService>();
        customerService.Setup(c => c.FindCustomerAsync(It.IsAny<BsonDocument>())).ReturnsAsync((CustomerEntity)null!);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new AddCustomerCommandHandler(mediator.Object, customerService.Object, eventStore.Object);

        var result = await handler.Handle(new AddCustomerCommand { CustomerEntity = customer }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        customerService.Verify(c => c.AddCustomerAsync(customer), Times.Once);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "AddCustomerCommand")), Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Creating a customer reaches no message broker and cannot be made to fail by one being absent.
    /// Topic-per-customer creation used to run here and returned a failure when no broker answered,
    /// which made an unreachable broker block signup outright.
    /// </summary>
    [Fact]
    public async Task Handle_SucceedsWithNoMessageBrokerAnywhere()
    {
        var customerService = new Mock<ICustomerService>();
        customerService.Setup(c => c.FindCustomerAsync(It.IsAny<BsonDocument>())).ReturnsAsync((CustomerEntity)null!);
        var handler = new AddCustomerCommandHandler(
            Mock.Of<IMediator>(), customerService.Object, Mock.Of<IEventStore>());

        var result = await handler.Handle(new AddCustomerCommand { CustomerEntity = Customer() }, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_DuplicateNameFound_ReturnsFailWithNoPublishAndNoAuditWrite()
    {
        // The duplicate check runs BEFORE mediator.Publish or any event store write -- a duplicate
        // never touches either.
        var customer = Customer();
        var customerService = new Mock<ICustomerService>();
        customerService.Setup(c => c.FindCustomerAsync(It.IsAny<BsonDocument>())).ReturnsAsync(Customer());
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new AddCustomerCommandHandler(mediator.Object, customerService.Object, eventStore.Object);

        var result = await handler.Handle(new AddCustomerCommand { CustomerEntity = customer }, CancellationToken.None);

        Assert.True(result.IsFailed);
        customerService.Verify(c => c.AddCustomerAsync(It.IsAny<CustomerEntity>()), Times.Never);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Never);
        eventStore.Verify(e => e.SaveAsync(It.IsAny<Event>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new AddCustomerCommandHandler(
            Mock.Of<IMediator>(), Mock.Of<ICustomerService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
