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

    // The address matching no PROVIDER used to be a hard failure, which the route turned into a 404 --
    // so a Customer asking for their own calendar always got 404, since appointments are embedded in the
    // provider's document and a customer never has a ProviderEntity. It now falls through to the
    // customer-side gather instead.
    [Fact]
    public async Task Handle_EmailBelongsToACustomer_ReturnsTheirOwnAppointmentsGatheredFromProviders()
    {
        var appointment = new AppointmentEntity
        {
            EmailProvider = ProviderEmail,
            EmailCustomer = CustomerEmail,
            Start = DateTime.UtcNow.AddDays(1),
            End = DateTime.UtcNow.AddDays(1).AddHours(1)
        };
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync((ProviderEntity)null!);
        providerService.Setup(p => p.FindAppointmentsByCustomerAsync(CustomerEmail)).ReturnsAsync([appointment]);
        var eventStore = new Mock<IEventStore>();
        var handler = new CheckCalendarAppointmentsQueryHandler(Mock.Of<IMediator>(), providerService.Object, eventStore.Object);

        var result = await handler.Handle(new CheckCalendarAppointmentsQuery { Email = CustomerEmail }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([appointment], result.Value);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "CheckCalendarAppointmentsQuery")), Times.Once);
    }

    // An address with no provider AND no bookings is a successful empty read, not a 404 -- matching the
    // route's own established "a provider with no appointments is not 'not found'" semantics.
    [Fact]
    public async Task Handle_EmailHasNoProviderAndNoAppointments_ReturnsOkWithAnEmptyList()
    {
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync((ProviderEntity)null!);
        providerService.Setup(p => p.FindAppointmentsByCustomerAsync(It.IsAny<string>())).ReturnsAsync([]);
        var handler = new CheckCalendarAppointmentsQueryHandler(Mock.Of<IMediator>(), providerService.Object, Mock.Of<IEventStore>());

        var result = await handler.Handle(new CheckCalendarAppointmentsQuery { Email = "nobody@example.com" }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new CheckCalendarAppointmentsQueryHandler(Mock.Of<IMediator>(), Mock.Of<IProviderService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
