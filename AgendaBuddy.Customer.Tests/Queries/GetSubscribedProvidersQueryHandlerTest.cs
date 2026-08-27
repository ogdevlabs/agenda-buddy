namespace AgendaBuddy.Customer.Tests.Queries;

public class GetSubscribedProvidersQueryHandlerTest
{
    private const string CustomerEmail = "customer@example.com";

    [Fact]
    public async Task Handle_CustomerExists_ReturnsSubscriptionList()
    {
        var customer = new CustomerEntity { Email = CustomerEmail, SubscribedProviderCollection = ["a@example.com", "b@example.com"] };
        var customerService = new Mock<ICustomerService>();
        customerService.Setup(c => c.FindCustomerAsync(It.IsAny<BsonDocument>())).ReturnsAsync(customer);
        var eventStore = new Mock<IEventStore>();
        var handler = new GetSubscribedProvidersQueryHandler(customerService.Object, eventStore.Object);

        var result = await handler.Handle(new GetSubscribedProvidersQuery { CustomerEmail = CustomerEmail }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["a@example.com", "b@example.com"], result.Value);
    }

    [Fact]
    public async Task Handle_NoSuchCustomer_ReturnsFail()
    {
        var customerService = new Mock<ICustomerService>();
        customerService.Setup(c => c.FindCustomerAsync(It.IsAny<BsonDocument>())).ReturnsAsync((CustomerEntity)null!);
        var eventStore = new Mock<IEventStore>();
        var handler = new GetSubscribedProvidersQueryHandler(customerService.Object, eventStore.Object);

        var result = await handler.Handle(new GetSubscribedProvidersQuery { CustomerEmail = "missing@example.com" }, CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
