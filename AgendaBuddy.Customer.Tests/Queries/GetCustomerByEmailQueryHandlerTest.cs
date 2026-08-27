namespace AgendaBuddy.Customer.Tests.Queries;

public class GetCustomerByEmailQueryHandlerTest
{
    private const string CustomerEmail = "customer@example.com";

    private static CustomerEntity Customer(string email) => new()
    {
        FirstName = "Ada",
        LastName = "Lovelace",
        Email = email
    };

    [Fact]
    public async Task Handle_CustomerExists_ReturnsOkWithCustomer()
    {
        var customer = Customer(CustomerEmail);
        var customerService = new Mock<ICustomerService>();
        customerService.Setup(c => c.FindCustomerAsync(It.IsAny<BsonDocument>())).ReturnsAsync(customer);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new GetCustomerByEmailQueryHandler(mediator.Object, customerService.Object, eventStore.Object);

        var result = await handler.Handle(new GetCustomerByEmailQuery { Email = CustomerEmail }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(customer, result.Value);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "GetCustomerByEmailQuery")), Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoSuchCustomer_ReturnsFailAndWritesFailedAudit()
    {
        var customerService = new Mock<ICustomerService>();
        customerService.Setup(c => c.FindCustomerAsync(It.IsAny<BsonDocument>())).ReturnsAsync((CustomerEntity)null!);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new GetCustomerByEmailQueryHandler(mediator.Object, customerService.Object, eventStore.Object);

        var result = await handler.Handle(new GetCustomerByEmailQuery { Email = "missing@example.com" }, CancellationToken.None);

        Assert.True(result.IsFailed);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed" && ev.Type == "GetCustomerByEmailQuery")), Times.Once);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new GetCustomerByEmailQueryHandler(Mock.Of<IMediator>(), Mock.Of<ICustomerService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
