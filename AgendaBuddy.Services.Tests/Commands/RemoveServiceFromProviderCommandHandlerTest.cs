namespace AgendaBuddy.Services.Tests.Commands;

public class RemoveServiceFromProviderCommandHandlerTest
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
    public async Task Handle_MatchingServiceExists_RemovesItAndReturnsOkWithUpdatedProvider()
    {
        var provider = Provider(ProviderEmail,
            new ServiceEntity("Massage", "60 minutes", 80m),
            new ServiceEntity("Consultation", "30 minutes", 20m));
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(provider);
        providerService.Setup(p => p.UpdateProviderAsync(provider.Id.ToString(), provider)).ReturnsAsync(true);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new RemoveServiceFromProviderCommandHandler(mediator.Object, providerService.Object, eventStore.Object);

        var command = new RemoveServiceFromProviderCommand { Email = ProviderEmail, ServiceName = "Massage" };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(provider.ServiceEntities, s => s.Name == "Massage");
        Assert.Single(provider.ServiceEntities);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "RemoveServiceFromProviderCommand")), Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoSuchService_ReturnsFailWithNoAuditWrite()
    {
        var provider = Provider(ProviderEmail, new ServiceEntity("Massage", "60 minutes", 80m));
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(provider);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new RemoveServiceFromProviderCommandHandler(mediator.Object, providerService.Object, eventStore.Object);

        var command = new RemoveServiceFromProviderCommand { Email = ProviderEmail, ServiceName = "Nonexistent" };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Single(provider.ServiceEntities);
        providerService.Verify(p => p.UpdateProviderAsync(It.IsAny<string>(), It.IsAny<ProviderEntity>()), Times.Never);
        eventStore.Verify(e => e.SaveAsync(It.IsAny<Event>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NoSuchProvider_ReturnsFailAndWritesFailedAudit()
    {
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync((ProviderEntity)null!);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new RemoveServiceFromProviderCommandHandler(mediator.Object, providerService.Object, eventStore.Object);

        var command = new RemoveServiceFromProviderCommand { Email = "missing@example.com", ServiceName = "Massage" };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailed);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed" && ev.Type == "RemoveServiceFromProviderCommand")), Times.Once);
    }

    [Fact]
    public async Task Handle_ProviderExistsButUpdateFails_ReturnsFailAndWritesFailedAudit()
    {
        var provider = Provider(ProviderEmail, new ServiceEntity("Massage", "60 minutes", 80m));
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(provider);
        providerService.Setup(p => p.UpdateProviderAsync(provider.Id.ToString(), provider)).ReturnsAsync(false);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new RemoveServiceFromProviderCommandHandler(mediator.Object, providerService.Object, eventStore.Object);

        var command = new RemoveServiceFromProviderCommand { Email = ProviderEmail, ServiceName = "Massage" };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailed);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed" && ev.Type == "RemoveServiceFromProviderCommand")), Times.Once);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new RemoveServiceFromProviderCommandHandler(Mock.Of<IMediator>(), Mock.Of<IProviderService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
