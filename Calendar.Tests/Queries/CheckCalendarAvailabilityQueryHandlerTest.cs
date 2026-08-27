namespace Calendar.Tests.Queries;

public class CheckCalendarAvailabilityQueryHandlerTest
{
    private static ProviderEntity Provider(string email) => new()
    {
        FirstName = "Test",
        LastName = "Provider",
        Email = email,
        AppointmentEntities = []
    };

    [Fact]
    public async Task Handle_ProviderExists_ReturnsOkWithSlots()
    {
        var provider = Provider("provider@example.com");
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(provider);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new CheckCalendarAvailabilityQueryHandler(mediator.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(new CheckCalendarAvailabilityQuery { Email = provider.Email }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "CheckCalendarAvailabilityQuery")), Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoSuchProvider_ReturnsFailAndWritesFailedAudit()
    {
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync((ProviderEntity)null!);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new CheckCalendarAvailabilityQueryHandler(mediator.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(new CheckCalendarAvailabilityQuery { Email = "missing@example.com" }, CancellationToken.None);

        Assert.True(result.IsFailed);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed" && ev.Type == "CheckCalendarAvailabilityQuery")), Times.Once);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new CheckCalendarAvailabilityQueryHandler(Mock.Of<IMediator>(), Mock.Of<IProviderService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
