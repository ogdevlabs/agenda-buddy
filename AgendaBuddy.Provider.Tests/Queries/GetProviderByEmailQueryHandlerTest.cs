namespace AgendaBuddy.Provider.Tests.Queries;

public class GetProviderByEmailQueryHandlerTest
{
    private const string ProviderEmail = "provider@example.com";

    private static ProviderEntity Provider(string email) => new()
    {
        FirstName = "Grace",
        LastName = "Hopper",
        Email = email
    };

    [Fact]
    public async Task Handle_ProviderExists_ReturnsOkWithProvider()
    {
        var provider = Provider(ProviderEmail);
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(provider);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new GetProviderByEmailQueryHandler(mediator.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(new GetProviderByEmailQuery { Email = ProviderEmail }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(provider, result.Value);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "GetProviderByEmailQuery")), Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoSuchProvider_ReturnsFailAndWritesFailedAudit()
    {
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync((ProviderEntity)null!);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new GetProviderByEmailQueryHandler(mediator.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(new GetProviderByEmailQuery { Email = "missing@example.com" }, CancellationToken.None);

        Assert.True(result.IsFailed);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed" && ev.Type == "GetProviderByEmailQuery")), Times.Once);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new GetProviderByEmailQueryHandler(Mock.Of<IMediator>(), Mock.Of<IProviderService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
