namespace Services.Tests.Commands;

public class AddServicesToProviderCommandHandlerTest
{
    private const string ProviderEmail = "provider@example.com";

    private static ProviderEntity Provider(string email) => new()
    {
        FirstName = "Test",
        LastName = "Provider",
        Email = email
    };

    [Fact]
    public async Task Handle_ProviderExistsAndUpdateSucceeds_ReturnsOkWithProvider()
    {
        var provider = Provider(ProviderEmail);
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(provider);
        providerService.Setup(p => p.UpdateProviderAsync(provider.Id.ToString(), provider)).ReturnsAsync(true);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new AddServicesToProviderCommandHandler(mediator.Object, providerService.Object, eventStore.Object);

        var command = new AddServicesToProviderCommand
        {
            Email = ProviderEmail,
            ServiceEntities = [new ServiceEntity("Massage", "60 minutes", 80m)]
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(provider, result.Value);
        Assert.Contains(provider.ServiceEntities, s => s.Name == "Massage");
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "AddServicesToProviderCommand")), Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoSuchProvider_ReturnsFailAndWritesFailedAudit()
    {
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync((ProviderEntity)null!);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new AddServicesToProviderCommandHandler(mediator.Object, providerService.Object, eventStore.Object);

        var command = new AddServicesToProviderCommand
        {
            Email = "missing@example.com",
            ServiceEntities = [new ServiceEntity("Massage", "60 minutes", 80m)]
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailed);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed" && ev.Type == "AddServicesToProviderCommand")), Times.Once);
    }

    [Fact]
    public async Task Handle_ProviderExistsButUpdateFails_ReturnsFailWithNoAuditWrite()
    {
        // Pre-existing gap (ServicesAuditTest's own remarks on the sibling handler): no audit write on
        // this branch. Pinning the behaviour, not the gap's absence of a fix.
        var provider = Provider(ProviderEmail);
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(provider);
        providerService.Setup(p => p.UpdateProviderAsync(provider.Id.ToString(), provider)).ReturnsAsync(false);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new AddServicesToProviderCommandHandler(mediator.Object, providerService.Object, eventStore.Object);

        var command = new AddServicesToProviderCommand
        {
            Email = ProviderEmail,
            ServiceEntities = [new ServiceEntity("Massage", "60 minutes", 80m)]
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailed);
        eventStore.Verify(e => e.SaveAsync(It.IsAny<Event>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new AddServicesToProviderCommandHandler(Mock.Of<IMediator>(), Mock.Of<IProviderService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
