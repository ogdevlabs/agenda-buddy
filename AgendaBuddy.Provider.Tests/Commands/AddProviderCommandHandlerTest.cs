namespace AgendaBuddy.Provider.Tests.Commands;

public class AddProviderCommandHandlerTest
{
    private static ProviderEntity Provider(string firstName = "Grace", string lastName = "Hopper", string email = "provider@example.com") => new()
    {
        FirstName = firstName,
        LastName = lastName,
        Email = email
    };

    [Fact]
    public async Task Handle_NoDuplicate_PersistsAndReturnsOk()
    {
        var provider = Provider();
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync((ProviderEntity)null!);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new AddProviderCommandHandler(mediator.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(new AddProviderCommand { ProviderEntity = provider }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        providerService.Verify(p => p.AddProviderAsync(provider), Times.Once);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "AddProviderCommand")), Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Creating a provider reaches no message broker and cannot be made to fail by one being absent.
    /// Topic-per-provider creation used to run here and returned a failure when no broker answered,
    /// which made an unreachable broker block signup outright.
    /// </summary>
    [Fact]
    public async Task Handle_SucceedsWithNoMessageBrokerAnywhere()
    {
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync((ProviderEntity)null!);
        var handler = new AddProviderCommandHandler(
            Mock.Of<IMediator>(), providerService.Object, Mock.Of<IEventStore>());

        var result = await handler.Handle(new AddProviderCommand { ProviderEntity = Provider() }, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_DuplicateNameFound_ReturnsFailWithNoPublishAndNoAuditWrite()
    {
        // The duplicate check runs BEFORE mediator.Publish or any event store write -- a duplicate
        // never touches either.
        var provider = Provider();
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(Provider());
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new AddProviderCommandHandler(mediator.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(new AddProviderCommand { ProviderEntity = provider }, CancellationToken.None);

        Assert.True(result.IsFailed);
        providerService.Verify(p => p.AddProviderAsync(It.IsAny<ProviderEntity>()), Times.Never);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Never);
        eventStore.Verify(e => e.SaveAsync(It.IsAny<Event>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new AddProviderCommandHandler(
            Mock.Of<IMediator>(), Mock.Of<IProviderService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
