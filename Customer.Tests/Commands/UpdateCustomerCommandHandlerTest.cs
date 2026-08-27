namespace Customer.Tests.Commands;

public class UpdateCustomerCommandHandlerTest
{
    private const string CustomerEmail = "customer@example.com";

    private static CustomerEntity Customer(string email) => new()
    {
        FirstName = "Ada",
        LastName = "Lovelace",
        Email = email
    };

    [Fact]
    public async Task Handle_RecordExistsAndUpdateSucceeds_ReturnsOkWithCustomer()
    {
        var existing = Customer(CustomerEmail);
        var updated = Customer(CustomerEmail);
        var customerService = new Mock<ICustomerService>();
        customerService.Setup(c => c.FindCustomerAsync(It.IsAny<BsonDocument>())).ReturnsAsync(existing);
        customerService.Setup(c => c.UpdateCustomerAsync(existing.Id.ToString(), updated)).ReturnsAsync(true);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new UpdateCustomerCommandHandler(mediator.Object, customerService.Object, eventStore.Object);

        var result = await handler.Handle(
            new UpdateCustomerCommand { Email = CustomerEmail, CustomerEntity = updated }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(updated, result.Value);
        Assert.Equal(existing.Id, updated.Id);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "UpdateCustomerCommand")), Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoSuchCustomer_ReturnsFailAndWritesFailedAudit()
    {
        var customerService = new Mock<ICustomerService>();
        customerService.Setup(c => c.FindCustomerAsync(It.IsAny<BsonDocument>())).ReturnsAsync((CustomerEntity)null!);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new UpdateCustomerCommandHandler(mediator.Object, customerService.Object, eventStore.Object);

        var result = await handler.Handle(
            new UpdateCustomerCommand { Email = "missing@example.com", CustomerEntity = Customer("missing@example.com") },
            CancellationToken.None);

        Assert.True(result.IsFailed);
        // Preserved verbatim from the pre-refactor handler: the audit Type here is literally
        // "UpdateProviderCommand" -- a pre-existing copy-paste defect out of this task's scope to fix
        // (F-018-T13's CustomerAuditTest remarks). Pinned here, not corrected.
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed" && ev.Type == "UpdateProviderCommand")), Times.Once);
    }

    [Fact]
    public async Task Handle_RecordExistsButUpdateFails_ReturnsFailWithNoAuditWrite()
    {
        // Pre-existing gap, preserved: no audit write on this branch, unlike the "record not found" branch.
        var existing = Customer(CustomerEmail);
        var updated = Customer(CustomerEmail);
        var customerService = new Mock<ICustomerService>();
        customerService.Setup(c => c.FindCustomerAsync(It.IsAny<BsonDocument>())).ReturnsAsync(existing);
        customerService.Setup(c => c.UpdateCustomerAsync(existing.Id.ToString(), updated)).ReturnsAsync(false);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new UpdateCustomerCommandHandler(mediator.Object, customerService.Object, eventStore.Object);

        var result = await handler.Handle(
            new UpdateCustomerCommand { Email = CustomerEmail, CustomerEntity = updated }, CancellationToken.None);

        Assert.True(result.IsFailed);
        eventStore.Verify(e => e.SaveAsync(It.IsAny<Event>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new UpdateCustomerCommandHandler(Mock.Of<IMediator>(), Mock.Of<ICustomerService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
