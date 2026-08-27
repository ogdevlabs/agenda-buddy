namespace Customer.Tests.Queries;

public class GetCustomersQueryHandlerTest
{
    private static CustomerEntity Customer(string email) => new()
    {
        FirstName = "Ada",
        LastName = "Lovelace",
        Email = email
    };

    [Fact]
    public async Task Handle_CustomersExist_ReturnsOkWithPagedResponseAndSuccessAudit()
    {
        var customers = new List<CustomerEntity> { Customer("a@example.com"), Customer("b@example.com") };
        var page = PageRequest.Clamp(1, 25);
        var customerService = new Mock<ICustomerService>();
        customerService.Setup(c => c.GetPagedCustomersAsync(page.Skip, page.PageSize))
            .ReturnsAsync(((IEnumerable<CustomerEntity>)customers, (long)customers.Count));
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new GetCustomersQueryHandler(mediator.Object, customerService.Object, eventStore.Object);

        var result = await handler.Handle(new GetCustomersQuery { Page = page }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, System.Linq.Enumerable.Count(result.Value.Items));
        Assert.Equal(2, result.Value.TotalCount);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "GetCustomersQuery")), Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoCustomers_StillReturnsOkWithEmptyPageButWritesFailedAudit()
    {
        // Preserves Customer/Program.cs's pre-existing behaviour: an empty page is a successful 200, never
        // a Result.Fail -- the "Failed" audit here is an AUDIT distinction only, not a control-flow one.
        var page = PageRequest.Clamp(1, 25);
        var customerService = new Mock<ICustomerService>();
        customerService.Setup(c => c.GetPagedCustomersAsync(page.Skip, page.PageSize))
            .ReturnsAsync(((IEnumerable<CustomerEntity>)new List<CustomerEntity>(), 0L));
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new GetCustomersQueryHandler(mediator.Object, customerService.Object, eventStore.Object);

        var result = await handler.Handle(new GetCustomersQuery { Page = page }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed" && ev.Type == "GetCustomersQuery")), Times.Once);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new GetCustomersQueryHandler(Mock.Of<IMediator>(), Mock.Of<ICustomerService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
