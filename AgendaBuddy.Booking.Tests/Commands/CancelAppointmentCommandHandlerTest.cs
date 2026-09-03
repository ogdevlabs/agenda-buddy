namespace AgendaBuddy.Booking.Tests.Commands;

// IBookingService/IProviderService already cover
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
        var handler = new CancelAppointmentCommandHandler(Mock.Of<IMediator>(), providers.Object, bookings.Object, eventStore.Object, Mock.Of<INotificationService>());

        var result = await handler.Handle(new CancelAppointmentCommand { Identifier = "abc123" }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success")), Times.Once);
    }

    [Fact]
    public async Task Handle_CompletedAppointment_RefusesToCancel_ReturnsFail()
    {
        // A completed appointment is history, not
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
        var handler = new CancelAppointmentCommandHandler(Mock.Of<IMediator>(), providers.Object, bookings.Object, eventStore.Object, Mock.Of<INotificationService>());

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
            Mock.Of<IMediator>(), Mock.Of<IProviderService>(), bookings.Object, eventStore.Object, Mock.Of<INotificationService>());

        var result = await handler.Handle(new CancelAppointmentCommand { Identifier = "missing" }, CancellationToken.None);

        Assert.True(result.IsFailed);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed")), Times.Once);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new CancelAppointmentCommandHandler(
            Mock.Of<IMediator>(), Mock.Of<IProviderService>(), Mock.Of<IBookingService>(), Mock.Of<IEventStore>(), Mock.Of<INotificationService>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
    // ── Notifications ─────────────────────────────────────────────────────────────────────────────
    // The command does not record who cancelled -- either party may -- so both are told rather than
    // guessing wrong about which side needs to know.

    [Fact]
    public async Task Handle_Cancelled_NotifiesBothParties()
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
        var notifications = new Mock<INotificationService>();

        var handler = new CancelAppointmentCommandHandler(
            Mock.Of<IMediator>(), providers.Object, bookings.Object, Mock.Of<IEventStore>(), notifications.Object);

        await handler.Handle(new CancelAppointmentCommand { Identifier = "abc123" }, CancellationToken.None);

        notifications.Verify(n => n.SendAsync(It.Is<NotificationEntity>(notification =>
            notification.RecipientEmail == "customer@example.com"
            && notification.Type == NotificationType.AppointmentCancelled)), Times.Once);
        notifications.Verify(n => n.SendAsync(It.Is<NotificationEntity>(notification =>
            notification.RecipientEmail == "provider@example.com"
            && notification.Type == NotificationType.AppointmentCancelled)), Times.Once);
    }

    // A notification is a courtesy on top of the cancellation, not a precondition for it. The appointment
    // is already cancelled by the time we try to send, so a failure here must not report failure to cancel.
    [Fact]
    public async Task Handle_NotificationThrows_StillSucceeds()
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
        var notifications = new Mock<INotificationService>();
        notifications.Setup(n => n.SendAsync(It.IsAny<NotificationEntity>()))
                     .ThrowsAsync(new InvalidOperationException("notification store down"));

        var handler = new CancelAppointmentCommandHandler(
            Mock.Of<IMediator>(), providers.Object, bookings.Object, Mock.Of<IEventStore>(), notifications.Object);

        var result = await handler.Handle(
            new CancelAppointmentCommand { Identifier = "abc123" }, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

}
