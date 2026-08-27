namespace AgendaBuddy.Customer.Tests.Commands;

public class UnsubscribeFromProviderCommandHandlerTest
{
    private const string CustomerEmail = "customer@example.com";
    private const string ProviderEmail = "provider@example.com";

    [Fact]
    public async Task Handle_CustomerExists_ReturnsOkAndCleansUpProviderSide()
    {
        var customer = new CustomerEntity { Email = CustomerEmail, SubscribedProviderCollection = [] };
        var customerService = new Mock<ICustomerService>();
        customerService.Setup(c => c.UnsubscribeFromProviderAsync(CustomerEmail, ProviderEmail)).ReturnsAsync(customer);
        var providerService = new Mock<IProviderService>();
        var eventStore = new Mock<IEventStore>();
        var handler = new UnsubscribeFromProviderCommandHandler(customerService.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(
            new UnsubscribeFromProviderCommand { CustomerEmail = CustomerEmail, ProviderEmail = ProviderEmail }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(customer, result.Value);
        providerService.Verify(p => p.UnsubscribeCustomerAsync(ProviderEmail, CustomerEmail), Times.Once);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "UnsubscribeFromProviderCommand")), Times.Once);
    }

    [Fact]
    public async Task Handle_ProviderNoLongerExists_StillSucceedsForTheCustomer()
    {
        // A since-deleted provider must not block a customer from clearing the stale reference out of
        // their own list -- the whole point of this handler's asymmetry.
        var customer = new CustomerEntity { Email = CustomerEmail, SubscribedProviderCollection = [] };
        var customerService = new Mock<ICustomerService>();
        customerService.Setup(c => c.UnsubscribeFromProviderAsync(CustomerEmail, ProviderEmail)).ReturnsAsync(customer);
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.UnsubscribeCustomerAsync(ProviderEmail, CustomerEmail)).ReturnsAsync((ProviderEntity)null!);
        var eventStore = new Mock<IEventStore>();
        var handler = new UnsubscribeFromProviderCommandHandler(customerService.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(
            new UnsubscribeFromProviderCommand { CustomerEmail = CustomerEmail, ProviderEmail = ProviderEmail }, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_NoSuchCustomer_ReturnsFailAndNeverTouchesProviderSide()
    {
        var customerService = new Mock<ICustomerService>();
        customerService.Setup(c => c.UnsubscribeFromProviderAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((CustomerEntity)null!);
        var providerService = new Mock<IProviderService>();
        var eventStore = new Mock<IEventStore>();
        var handler = new UnsubscribeFromProviderCommandHandler(customerService.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(
            new UnsubscribeFromProviderCommand { CustomerEmail = "missing@example.com", ProviderEmail = ProviderEmail }, CancellationToken.None);

        Assert.True(result.IsFailed);
        providerService.Verify(p => p.UnsubscribeCustomerAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed")), Times.Once);
    }
}
