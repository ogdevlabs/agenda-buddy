namespace Provider.Tests.Commands;

public class DeactivateProviderCommandHandlerTest
{
    private const string ProviderEmail = "provider@example.com";

    private static ProviderEntity Provider(string email) => new()
    {
        FirstName = "Grace",
        LastName = "Hopper",
        Email = email,
        IsActive = true
    };

    [Fact]
    public async Task Handle_ProviderExists_SetsInactiveAndReturnsOk()
    {
        var requestEntity = Provider(ProviderEmail);
        var stored = Provider(ProviderEmail);
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(stored);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new DeactivateProviderCommandHandler(mediator.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(new DeactivateProviderCommand { ProviderEntity = requestEntity }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsActive);
        providerService.Verify(p => p.SetActiveAsync(ProviderEmail, false), Times.Once);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "DeactivateProviderCommand")), Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoSuchProvider_ReturnsFailAndWritesFailedAudit()
    {
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync((ProviderEntity)null!);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new DeactivateProviderCommandHandler(mediator.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(
            new DeactivateProviderCommand { ProviderEntity = Provider("missing@example.com") }, CancellationToken.None);

        Assert.True(result.IsFailed);
        providerService.Verify(p => p.SetActiveAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed" && ev.Type == "DeactivateProviderCommand")), Times.Once);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new DeactivateProviderCommandHandler(Mock.Of<IMediator>(), Mock.Of<IProviderService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
