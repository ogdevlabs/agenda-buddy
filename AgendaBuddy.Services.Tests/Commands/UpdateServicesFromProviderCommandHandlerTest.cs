namespace AgendaBuddy.Services.Tests.Commands;

public class UpdateServicesFromProviderCommandHandlerTest
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
    public async Task Handle_MatchingServiceExistsAndUpdateSucceeds_ReturnsOkWithUpdatedProvider()
    {
        var existingService = new ServiceEntity("Massage", "60 minutes", 80m);
        var provider = Provider(ProviderEmail, existingService);
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(provider);
        providerService.Setup(p => p.UpdateProviderAsync(provider.Id.ToString(), provider)).ReturnsAsync(true);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new UpdateServicesFromProviderCommandHandler(mediator.Object, providerService.Object, eventStore.Object);

        var command = new UpdateServicesFromProviderCommand
        {
            Email = ProviderEmail,
            ServiceEntities = [new ServiceEntity("Massage", "90 minutes", 120m)]
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("90 minutes", existingService.Description);
        Assert.Equal(120m, existingService.Fee);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "UpdateServicesFromProviderCommand")), Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoSuchProvider_ReturnsFailWithNoAuditWrite()
    {
        // Pre-existing gap, documented by ServicesAuditTest: this branch writes no audit event at all,
        // unlike AddServicesToProviderCommandHandler's equivalent. Pinning the behaviour as-is.
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync((ProviderEntity)null!);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new UpdateServicesFromProviderCommandHandler(mediator.Object, providerService.Object, eventStore.Object);

        var command = new UpdateServicesFromProviderCommand
        {
            Email = "missing@example.com",
            ServiceEntities = [new ServiceEntity("Massage", "90 minutes", 120m)]
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailed);
        eventStore.Verify(e => e.SaveAsync(It.IsAny<Event>()), Times.Never);
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
        var handler = new UpdateServicesFromProviderCommandHandler(mediator.Object, providerService.Object, eventStore.Object);

        var command = new UpdateServicesFromProviderCommand
        {
            Email = ProviderEmail,
            ServiceEntities = [new ServiceEntity("Massage", "90 minutes", 120m)]
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailed);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed" && ev.Type == "UpdateServicesFromProviderCommand")), Times.Once);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new UpdateServicesFromProviderCommandHandler(Mock.Of<IMediator>(), Mock.Of<IProviderService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
