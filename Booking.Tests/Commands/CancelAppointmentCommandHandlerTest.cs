namespace Booking.Tests.Commands;

// F-019 Party Review (Echo's Critical finding). IBookingService/IProviderService already cover
// everything this handler calls -- retyped from the concrete classes, so this is now real
// Moq-based business-logic coverage, not just a GuardClause-null check.
public class CancelAppointmentCommandHandlerTest
{
    private static AppointmentEntity MakeAppointment(
        string identifier = "abc123", AppointmentStatus status = AppointmentStatus.Requested) => new()
        {
            Identifier = identifier,
            EmailProvider = "provider@example.com",
            EmailCustomer = "customer@example.com",
            AppointmentStatus = status
        };

    [Fact]
    public async Task Handle_BookedAppointment_CancelsAndReturnsOk()
    {
        var appointment = MakeAppointment(status: AppointmentStatus.Booked);
        var providerEntity = new ProviderEntity
        {
            Id = ObjectId.GenerateNewId(),
            Email = "provider@example.com",
            AppointmentEntities = [appointment]
        };
        var bookings = new Mock<IBookingService>();
        bookings.Setup(b => b.SearchAppointmentAsync("abc123")).ReturnsAsync(appointment);
        bookings.Setup(b => b.CancelAppointmentAsync("abc123")).ReturnsAsync(true);
        var providers = new Mock<IProviderService>();
        providers.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(providerEntity);
        providers.Setup(p => p.UpdateProviderAsync(providerEntity.Id.ToString(), providerEntity)).ReturnsAsync(true);
        var eventStore = new Mock<IEventStore>();
        var handler = new CancelAppointmentCommandHandler(Mock.Of<IMediator>(), providers.Object, bookings.Object, eventStore.Object);

        var result = await handler.Handle(new CancelAppointmentCommand { Identifier = "abc123" }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success")), Times.Once);
    }

    [Fact]
    public async Task Handle_CompletedAppointment_RefusesToCancel_ReturnsFail()
    {
        // F-014 requirement 15 / Discover finding F-3: a completed appointment is history, not
        // cancellable -- the opposite of the original (backwards) rule this codebase used to have.
        var appointment = MakeAppointment(status: AppointmentStatus.Completed);
        var providerEntity = new ProviderEntity
        {
            Id = ObjectId.GenerateNewId(),
            Email = "provider@example.com",
            AppointmentEntities = [appointment]
        };
        var bookings = new Mock<IBookingService>();
        bookings.Setup(b => b.SearchAppointmentAsync("abc123")).ReturnsAsync(appointment);
        var providers = new Mock<IProviderService>();
        providers.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(providerEntity);
        var eventStore = new Mock<IEventStore>();
        var handler = new CancelAppointmentCommandHandler(Mock.Of<IMediator>(), providers.Object, bookings.Object, eventStore.Object);

        var result = await handler.Handle(new CancelAppointmentCommand { Identifier = "abc123" }, CancellationToken.None);

        Assert.True(result.IsFailed);
        bookings.Verify(b => b.CancelAppointmentAsync(It.IsAny<string>()), Times.Never);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed")), Times.Once);
    }

    [Fact]
    public async Task Handle_NoSuchAppointment_ReturnsFail()
    {
        var bookings = new Mock<IBookingService>();
        bookings.Setup(b => b.SearchAppointmentAsync("missing")).ReturnsAsync((AppointmentEntity?)null);
        var eventStore = new Mock<IEventStore>();
        var handler = new CancelAppointmentCommandHandler(
            Mock.Of<IMediator>(), Mock.Of<IProviderService>(), bookings.Object, eventStore.Object);

        var result = await handler.Handle(new CancelAppointmentCommand { Identifier = "missing" }, CancellationToken.None);

        Assert.True(result.IsFailed);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed")), Times.Once);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new CancelAppointmentCommandHandler(
            Mock.Of<IMediator>(), Mock.Of<IProviderService>(), Mock.Of<IBookingService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
