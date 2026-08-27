namespace AgendaBuddy.Customer.Tests.Commands;

public class SubscribeToProviderCommandHandlerTest
{
    private const string CustomerEmail = "customer@example.com";
    private const string ProviderEmail = "provider@example.com";

    [Fact]
    public async Task Handle_ProviderAndCustomerExist_ReturnsOkWithUpdatedCustomer()
    {
        var provider = new ProviderEntity { Email = ProviderEmail };
        var customer = new CustomerEntity { Email = CustomerEmail, SubscribedProviderCollection = [ProviderEmail] };
        var customerService = new Mock<ICustomerService>();
        customerService.Setup(c => c.SubscribeToProviderAsync(CustomerEmail, ProviderEmail)).ReturnsAsync(customer);
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.SubscribeCustomerAsync(ProviderEmail, CustomerEmail)).ReturnsAsync(provider);
        var eventStore = new Mock<IEventStore>();
        var handler = new SubscribeToProviderCommandHandler(customerService.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(
            new SubscribeToProviderCommand { CustomerEmail = CustomerEmail, ProviderEmail = ProviderEmail }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(customer, result.Value);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "SubscribeToProviderCommand")), Times.Once);
    }

    [Fact]
    public async Task Handle_NoSuchProvider_ReturnsFailAndNeverWritesTheCustomer()
    {
        var customerService = new Mock<ICustomerService>();
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.SubscribeCustomerAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((ProviderEntity)null!);
        var eventStore = new Mock<IEventStore>();
        var handler = new SubscribeToProviderCommandHandler(customerService.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(
            new SubscribeToProviderCommand { CustomerEmail = CustomerEmail, ProviderEmail = "missing@example.com" }, CancellationToken.None);

        Assert.True(result.IsFailed);
        customerService.Verify(c => c.SubscribeToProviderAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed")), Times.Once);
    }

    [Fact]
    public async Task Handle_NoSuchCustomer_ReturnsFail()
    {
        var provider = new ProviderEntity { Email = ProviderEmail };
        var customerService = new Mock<ICustomerService>();
        customerService.Setup(c => c.SubscribeToProviderAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((CustomerEntity)null!);
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.SubscribeCustomerAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(provider);
        var eventStore = new Mock<IEventStore>();
        var handler = new SubscribeToProviderCommandHandler(customerService.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(
            new SubscribeToProviderCommand { CustomerEmail = "missing@example.com", ProviderEmail = ProviderEmail }, CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
