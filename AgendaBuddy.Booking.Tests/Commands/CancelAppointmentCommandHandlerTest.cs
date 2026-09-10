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
        bookings.Setup(b => b.CancelAppointmentAsync("abc123", It.IsAny<DateTime?>())).ReturnsAsync(true);
        var providers = new Mock<IProviderService>();
        providers.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(providerEntity);
        providers.Setup(p => p.ChangeEmbeddedAppointmentStatusAsync(
                       providerEntity.Email, "abc123", AppointmentStatus.Cancelled, It.IsAny<string>()))
                 .ReturnsAsync(providerEntity);
        var eventStore = new Mock<IEventStore>();
        var payments = new Mock<IPaymentService>();
        payments.Setup(p => p.ReleaseOrRefundAsync("abc123")).ReturnsAsync(true);
        var handler = new CancelAppointmentCommandHandler(Mock.Of<IMediator>(), providers.Object, bookings.Object, payments.Object, eventStore.Object, Mock.Of<INotificationDispatcher>());

        var result = await handler.Handle(new CancelAppointmentCommand { Identifier = "abc123", CancelledByEmail = "provider@example.com" }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success")), Times.Once);
    }

    [Fact]
    public async Task Handle_CompletedAppointment_RefusesToCancel_ReturnsFail()
    {
        // A completed appointment is history, not cancellable -- the opposite of the original (backwards) rule
        // this codebase used to have.
        //
        // The rule now lives in BookingService.CancelAppointmentAsync's FILTER, not in a check here, so that
        // the check and the write are one atomic operation: a preceding read could see Booked, be overtaken by
        // a completion, and then cancel work that had already been delivered. So the handler DOES call the
        // service, the service matches nothing, and the command fails -- which is what this asserts. The filter
        // itself is covered by BookingServiceTest.
        var appointment = MakeAppointment(status: AppointmentStatus.Completed);
        var bookings = new Mock<IBookingService>();
        bookings.Setup(b => b.SearchAppointmentAsync("abc123")).ReturnsAsync(appointment);
        bookings.Setup(b => b.CancelAppointmentAsync("abc123", It.IsAny<DateTime?>())).ReturnsAsync(false);
        var providers = new Mock<IProviderService>();
        var eventStore = new Mock<IEventStore>();
        var handler = new CancelAppointmentCommandHandler(Mock.Of<IMediator>(), providers.Object, bookings.Object, Mock.Of<IPaymentService>(), eventStore.Object, Mock.Of<INotificationDispatcher>());

        var result = await handler.Handle(new CancelAppointmentCommand { Identifier = "abc123", CancelledByEmail = "provider@example.com" }, CancellationToken.None);

        Assert.True(result.IsFailed);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed")), Times.Once);

        // Nothing was written to the embedded copy either, so the two stores cannot disagree.
        providers.Verify(p => p.ChangeEmbeddedAppointmentStatusAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AppointmentStatus>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NoSuchAppointment_ReturnsFail()
    {
        var bookings = new Mock<IBookingService>();
        bookings.Setup(b => b.SearchAppointmentAsync("missing")).ReturnsAsync((AppointmentEntity?)null);
        var eventStore = new Mock<IEventStore>();
        var handler = new CancelAppointmentCommandHandler(
            Mock.Of<IMediator>(), Mock.Of<IProviderService>(), bookings.Object, Mock.Of<IPaymentService>(), eventStore.Object, Mock.Of<INotificationDispatcher>());

        var result = await handler.Handle(new CancelAppointmentCommand { Identifier = "missing", CancelledByEmail = "provider@example.com" }, CancellationToken.None);

        Assert.True(result.IsFailed);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed")), Times.Once);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new CancelAppointmentCommandHandler(
            Mock.Of<IMediator>(), Mock.Of<IProviderService>(), Mock.Of<IBookingService>(), Mock.Of<IPaymentService>(), Mock.Of<IEventStore>(), Mock.Of<INotificationDispatcher>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
    // ── Notifications ─────────────────────────────────────────────────────────────────────────────
    // The command does not record who cancelled -- either party may -- so both are told rather than
    // guessing wrong about which side needs to know. Each is told through INotificationDispatcher, which
    // fans out to email and push as well as the in-app inbox: a cancellation that only lands in an inbox
    // behind a login reaches whichever party is not currently in the app not at all.

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
        bookings.Setup(b => b.CancelAppointmentAsync("abc123", It.IsAny<DateTime?>())).ReturnsAsync(true);
        var providers = new Mock<IProviderService>();
        providers.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(providerEntity);
        providers.Setup(p => p.ChangeEmbeddedAppointmentStatusAsync(
                       providerEntity.Email, "abc123", AppointmentStatus.Cancelled, It.IsAny<string>()))
                 .ReturnsAsync(providerEntity);
        var notifications = new Mock<INotificationDispatcher>();
        var payments = new Mock<IPaymentService>();
        payments.Setup(p => p.ReleaseOrRefundAsync("abc123")).ReturnsAsync(true);

        var handler = new CancelAppointmentCommandHandler(
            Mock.Of<IMediator>(), providers.Object, bookings.Object, payments.Object, Mock.Of<IEventStore>(), notifications.Object);

        await handler.Handle(new CancelAppointmentCommand { Identifier = "abc123", CancelledByEmail = "provider@example.com" }, CancellationToken.None);

        // A body each, naming the OTHER party: one shared body left neither side able to tell which of their
        // appointments it was about.
        notifications.Verify(n => n.DispatchAsync(It.Is<NotificationEntity>(notification =>
            notification.RecipientEmail == "customer@example.com"
            && notification.Type == NotificationType.AppointmentCancelled
            && notification.Body.Contains("provider@example.com")), It.IsAny<CancellationToken>()), Times.Once);
        notifications.Verify(n => n.DispatchAsync(It.Is<NotificationEntity>(notification =>
            notification.RecipientEmail == "provider@example.com"
            && notification.Type == NotificationType.AppointmentCancelled
            && notification.Body.Contains("customer@example.com")), It.IsAny<CancellationToken>()), Times.Once);
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
        bookings.Setup(b => b.CancelAppointmentAsync("abc123", It.IsAny<DateTime?>())).ReturnsAsync(true);
        var providers = new Mock<IProviderService>();
        providers.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(providerEntity);
        providers.Setup(p => p.ChangeEmbeddedAppointmentStatusAsync(
                       providerEntity.Email, "abc123", AppointmentStatus.Cancelled, It.IsAny<string>()))
                 .ReturnsAsync(providerEntity);
        // The dispatcher is contracted never to throw, but this asserts the handler does not DEPEND on that:
        // the invariant being protected is the cancellation, and it is not the dispatcher's to keep.
        var notifications = new Mock<INotificationDispatcher>();
        notifications.Setup(n => n.DispatchAsync(It.IsAny<NotificationEntity>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new InvalidOperationException("notification store down"));
        var payments = new Mock<IPaymentService>();
        payments.Setup(p => p.ReleaseOrRefundAsync("abc123")).ReturnsAsync(true);

        var handler = new CancelAppointmentCommandHandler(
            Mock.Of<IMediator>(), providers.Object, bookings.Object, payments.Object, Mock.Of<IEventStore>(), notifications.Object);

        var result = await handler.Handle(
            new CancelAppointmentCommand { Identifier = "abc123", CancelledByEmail = "provider@example.com" }, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ProviderCancellation_ReleasesPaymentBeforeCancelling()
    {
        var appointment = MakeAppointment(status: AppointmentStatus.Booked);
        var sequence = new MockSequence();
        var payments = new Mock<IPaymentService>();
        payments.InSequence(sequence).Setup(p => p.ReleaseOrRefundAsync("abc123")).ReturnsAsync(true);
        var bookings = new Mock<IBookingService>();
        bookings.Setup(b => b.SearchAppointmentAsync("abc123")).ReturnsAsync(appointment);
        bookings.InSequence(sequence).Setup(b => b.CancelAppointmentAsync("abc123", null)).ReturnsAsync(true);
        var providers = new Mock<IProviderService>();
        providers.Setup(p => p.ChangeEmbeddedAppointmentStatusAsync(
                appointment.EmailProvider, "abc123", AppointmentStatus.Cancelled, It.IsAny<string>()))
            .ReturnsAsync(new ProviderEntity());
        var handler = new CancelAppointmentCommandHandler(
            Mock.Of<IMediator>(), providers.Object, bookings.Object, payments.Object,
            Mock.Of<IEventStore>(), Mock.Of<INotificationDispatcher>());

        var result = await handler.Handle(new CancelAppointmentCommand
        {
            Identifier = "abc123",
            CancelledByEmail = appointment.EmailProvider
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ProviderCancellation_WhenReleaseFails_LeavesAppointmentBooked()
    {
        var appointment = MakeAppointment(status: AppointmentStatus.Booked);
        var payments = new Mock<IPaymentService>();
        payments.Setup(p => p.ReleaseOrRefundAsync("abc123")).ReturnsAsync(false);
        var bookings = new Mock<IBookingService>();
        bookings.Setup(b => b.SearchAppointmentAsync("abc123")).ReturnsAsync(appointment);
        var providers = new Mock<IProviderService>();
        var handler = new CancelAppointmentCommandHandler(
            Mock.Of<IMediator>(), providers.Object, bookings.Object, payments.Object,
            Mock.Of<IEventStore>(), Mock.Of<INotificationDispatcher>());

        var result = await handler.Handle(new CancelAppointmentCommand
        {
            Identifier = "abc123",
            CancelledByEmail = appointment.EmailProvider
        }, CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Contains("hold could not be released", result.Errors[0].Message);
        bookings.Verify(b => b.CancelAppointmentAsync(It.IsAny<string>(), It.IsAny<DateTime?>()), Times.Never);
        providers.Verify(p => p.ChangeEmbeddedAppointmentStatusAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AppointmentStatus>(), It.IsAny<string>()), Times.Never);
    }

}
