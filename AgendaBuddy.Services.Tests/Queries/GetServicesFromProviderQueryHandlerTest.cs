namespace AgendaBuddy.Services.Tests.Queries;

public class GetServicesFromProviderQueryHandlerTest
{
    private const string ProviderEmail = "provider@example.com";

    private static ProviderEntity Provider(string email, params ServiceEntity[] services) => new()
    {
        FirstName = "Test",
        LastName = "Provider",
        Email = email,
        ServiceEntities = [.. services]
    };

    [Fact]
    public async Task Handle_ProviderExists_ReturnsOkWithServices()
    {
        var service = new ServiceEntity("Massage", "60 minutes", 80m);
        var provider = Provider(ProviderEmail, service);
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(provider);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new GetServicesFromProviderQueryHandler(mediator.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(new GetServicesFromProviderQuery { Email = ProviderEmail }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([service], result.Value);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "GetServicesFromProviderQuery")), Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoSuchProvider_ReturnsOkWithEmptyListAndWritesFailedAudit()
    {
        // Preserves the pre-existing behaviour: a missing provider is a successful EMPTY read, not a
        // Result.Fail -- see the handler's own remarks.
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync((ProviderEntity)null!);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new GetServicesFromProviderQueryHandler(mediator.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(new GetServicesFromProviderQuery { Email = "missing@example.com" }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed" && ev.Type == "GetServicesFromProviderQuery")), Times.Once);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new GetServicesFromProviderQueryHandler(Mock.Of<IMediator>(), Mock.Of<IProviderService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
