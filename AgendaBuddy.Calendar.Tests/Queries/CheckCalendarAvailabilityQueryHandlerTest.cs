namespace AgendaBuddy.Calendar.Tests.Queries;

public class CheckCalendarAvailabilityQueryHandlerTest
{
    private static ProviderEntity Provider(string email) => new()
    {
        FirstName = "Test",
        LastName = "Provider",
        Email = email,
        AppointmentEntities = []
    };

    /// <summary>A provider with no time off recorded, which is what makes the slot assertions below stable.</summary>
    private static Mock<ICalendarBlockService> BlockService(params CalendarBlockEntity[] blocks)
    {
        var service = new Mock<ICalendarBlockService>();
        service.Setup(s => s.GetBlocksAsync(It.IsAny<string>(), It.IsAny<DateTime>())).ReturnsAsync(blocks);
        return service;
    }

    [Fact]
    public async Task Handle_ProviderExists_ReturnsOkWithSlots()
    {
        var provider = Provider("provider@example.com");
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(provider);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new CheckCalendarAvailabilityQueryHandler(
            mediator.Object, providerService.Object, BlockService().Object, eventStore.Object);

        var result = await handler.Handle(new CheckCalendarAvailabilityQuery { Email = provider.Email }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "CheckCalendarAvailabilityQuery")), Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// The handler is the ONLY thing that joins a provider's time off to the availability they advertise. The
    /// calculator takes blocks as an optional argument, so a handler that forgets to pass them still compiles and
    /// still returns slots — it just offers the customer times the provider is away.
    /// </summary>
    [Fact]
    public async Task Handle_TimeOff_IsSubtractedFromTheSlotsOffered()
    {
        var provider = Provider("provider@example.com");
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(provider);
        var mediator = new Mock<IMediator>();

        var unblocked = await new CheckCalendarAvailabilityQueryHandler(
                mediator.Object, providerService.Object, BlockService().Object, Mock.Of<IEventStore>())
            .Handle(new CheckCalendarAvailabilityQuery { Email = provider.Email, Days = 7 }, CancellationToken.None);

        // A block wide enough to cover the whole window, so this asserts on the mechanism rather than on which
        // hours the default working day happens to produce.
        var wholeWindow = new CalendarBlockEntity(
            provider.Email, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), "sabbatical");

        var blocked = await new CheckCalendarAvailabilityQueryHandler(
                mediator.Object, providerService.Object, BlockService(wholeWindow).Object, Mock.Of<IEventStore>())
            .Handle(new CheckCalendarAvailabilityQuery { Email = provider.Email, Days = 7 }, CancellationToken.None);

        Assert.NotEmpty(unblocked.Value);
        Assert.Empty(blocked.Value);
    }

    /// <summary>
    /// Blocks must be read for the provider whose calendar is being asked about — not the caller, and not a
    /// hardcoded address. Reading the wrong one leaks one provider's time off into another's calendar.
    /// </summary>
    [Fact]
    public async Task Handle_ReadsTimeOffForTheProviderBeingAskedAbout()
    {
        var provider = Provider("provider@example.com");
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(provider);
        var blocks = BlockService();

        await new CheckCalendarAvailabilityQueryHandler(
                Mock.Of<IMediator>(), providerService.Object, blocks.Object, Mock.Of<IEventStore>())
            .Handle(new CheckCalendarAvailabilityQuery { Email = provider.Email }, CancellationToken.None);

        blocks.Verify(s => s.GetBlocksAsync(provider.Email, It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoSuchProvider_ReturnsFailAndWritesFailedAudit()
    {
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync((ProviderEntity)null!);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new CheckCalendarAvailabilityQueryHandler(
            mediator.Object, providerService.Object, BlockService().Object, eventStore.Object);

        var result = await handler.Handle(new CheckCalendarAvailabilityQuery { Email = "missing@example.com" }, CancellationToken.None);

        Assert.True(result.IsFailed);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed" && ev.Type == "CheckCalendarAvailabilityQuery")), Times.Once);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new CheckCalendarAvailabilityQueryHandler(
            Mock.Of<IMediator>(), Mock.Of<IProviderService>(), BlockService().Object, Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
