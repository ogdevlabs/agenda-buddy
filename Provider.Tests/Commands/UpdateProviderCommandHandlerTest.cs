namespace Provider.Tests.Commands;

public class UpdateProviderCommandHandlerTest
{
    private const string ProviderEmail = "provider@example.com";

    private static ProviderEntity Provider(string email) => new()
    {
        FirstName = "Grace",
        LastName = "Hopper",
        Email = email
    };

    [Fact]
    public async Task Handle_RecordExistsAndUpdateSucceeds_ReturnsOkWithProvider()
    {
        var existing = Provider(ProviderEmail);
        var updated = Provider(ProviderEmail);
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(existing);
        providerService.Setup(p => p.UpdateProviderAsync(existing.Id.ToString(), updated)).ReturnsAsync(true);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new UpdateProviderCommandHandler(mediator.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(
            new UpdateProviderCommand { Email = ProviderEmail, ProviderEntity = updated }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(updated, result.Value);
        Assert.Equal(existing.Id, updated.Id);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "UpdateProviderCommand")), Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoSuchProvider_ReturnsFailAndWritesFailedAudit()
    {
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync((ProviderEntity)null!);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new UpdateProviderCommandHandler(mediator.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(
            new UpdateProviderCommand { Email = "missing@example.com", ProviderEntity = Provider("missing@example.com") },
            CancellationToken.None);

        Assert.True(result.IsFailed);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed" && ev.Type == "UpdateProviderCommand")), Times.Once);
    }

    [Fact]
    public async Task Handle_RecordExistsButUpdateFails_ReturnsFailWithNoAuditWrite()
    {
        // Pre-existing gap, preserved: no audit write on this branch, unlike the "record not found" branch.
        var existing = Provider(ProviderEmail);
        var updated = Provider(ProviderEmail);
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(existing);
        providerService.Setup(p => p.UpdateProviderAsync(existing.Id.ToString(), updated)).ReturnsAsync(false);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new UpdateProviderCommandHandler(mediator.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(
            new UpdateProviderCommand { Email = ProviderEmail, ProviderEntity = updated }, CancellationToken.None);

        Assert.True(result.IsFailed);
        eventStore.Verify(e => e.SaveAsync(It.IsAny<Event>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new UpdateProviderCommandHandler(Mock.Of<IMediator>(), Mock.Of<IProviderService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
