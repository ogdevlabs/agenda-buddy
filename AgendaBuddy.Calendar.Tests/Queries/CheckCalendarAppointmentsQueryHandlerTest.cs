namespace AgendaBuddy.Calendar.Tests.Queries;

public class CheckCalendarAppointmentsQueryHandlerTest
{
    private const string ProviderEmail = "provider@example.com";
    private const string CustomerEmail = "customer@example.com";

    [Fact]
    public async Task Handle_ProviderExists_ReturnsOkWithAppointments()
    {
        var appointment = new AppointmentEntity
        {
            EmailProvider = ProviderEmail,
            EmailCustomer = CustomerEmail,
            Start = DateTime.UtcNow.AddDays(1),
            End = DateTime.UtcNow.AddDays(1).AddHours(1)
        };
        var provider = new ProviderEntity
        {
            FirstName = "Test",
            LastName = "Provider",
            Email = ProviderEmail,
            AppointmentEntities = [appointment]
        };
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(provider);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new CheckCalendarAppointmentsQueryHandler(mediator.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(new CheckCalendarAppointmentsQuery { Email = ProviderEmail }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([appointment], result.Value);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "CheckCalendarAppointmentsQuery")), Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoSuchProvider_ReturnsFailAndWritesFailedAudit()
    {
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync((ProviderEntity)null!);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new CheckCalendarAppointmentsQueryHandler(mediator.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(new CheckCalendarAppointmentsQuery { Email = "missing@example.com" }, CancellationToken.None);

        Assert.True(result.IsFailed);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed" && ev.Type == "CheckCalendarAppointmentsQuery")), Times.Once);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new CheckCalendarAppointmentsQueryHandler(Mock.Of<IMediator>(), Mock.Of<IProviderService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
