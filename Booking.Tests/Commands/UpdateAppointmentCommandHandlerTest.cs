namespace Booking.Tests.Commands;

// F-019 Party Review (Echo's Critical finding). IBookingService/IProviderService already cover
// everything this handler calls -- retyped from the concrete classes, so this is now real
// Moq-based business-logic coverage, not just a GuardClause-null check.
public class UpdateAppointmentCommandHandlerTest
{
    private static AppointmentEntity MakeAppointment(string identifier = "abc123") => new()
    {
        Identifier = identifier,
        EmailProvider = "provider@example.com",
        EmailCustomer = "customer@example.com",
        Start = DateTime.UtcNow.AddHours(1),
        End = DateTime.UtcNow.AddHours(2),
        // A forged status the caller must never see echoed back (agenda-buddy-2hd).
        AppointmentStatus = AppointmentStatus.Completed
    };

    [Fact]
    public async Task Handle_ProviderAndAppointmentFound_UpdatesAndReturnsThePersistedEntity_NotTheRequestOne()
    {
        var providerEntity = new ProviderEntity
        {
            Id = ObjectId.GenerateNewId(),
            Email = "provider@example.com",
            AppointmentEntities =
            [
                new AppointmentEntity
                {
                    Identifier = "abc123", EmailProvider = "provider@example.com",
                    EmailCustomer = "customer@example.com",
                    AppointmentStatus = AppointmentStatus.Requested
                }
            ]
        };
        var providers = new Mock<IProviderService>();
        providers.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(providerEntity);
        providers.Setup(p => p.UpdateProviderAsync(providerEntity.Id.ToString(), providerEntity)).ReturnsAsync(true);
        var bookings = new Mock<IBookingService>();
        bookings.Setup(b => b.UpdateAppointmentAsync("abc123", It.IsAny<AppointmentEntity>())).ReturnsAsync(true);
        var eventStore = new Mock<IEventStore>();
        var handler = new UpdateAppointmentCommandHandler(Mock.Of<IMediator>(), providers.Object, bookings.Object, eventStore.Object);

        var result = await handler.Handle(new UpdateAppointmentCommand { AppointmentEntity = MakeAppointment() }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        // The stored copy, never mutated to Completed by this handler -- proves the response reflects
        // reality, not the caller's forged submission.
        Assert.Equal(AppointmentStatus.Requested, result.Value.AppointmentStatus);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success")), Times.Once);
    }

    [Fact]
    public async Task Handle_NoSuchProvider_ReturnsFail_AndWritesAFailedAuditEvent()
    {
        var providers = new Mock<IProviderService>();
        providers.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync((ProviderEntity?)null);
        var eventStore = new Mock<IEventStore>();
        var handler = new UpdateAppointmentCommandHandler(
            Mock.Of<IMediator>(), providers.Object, Mock.Of<IBookingService>(), eventStore.Object);

        var result = await handler.Handle(new UpdateAppointmentCommand { AppointmentEntity = MakeAppointment() }, CancellationToken.None);

        Assert.True(result.IsFailed);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed")), Times.Once);
    }

    [Fact]
    public async Task Handle_NoSuchAppointmentOnProvider_ReturnsFail()
    {
        var providerEntity = new ProviderEntity
        {
            Id = ObjectId.GenerateNewId(),
            Email = "provider@example.com",
            AppointmentEntities = []
        };
        var providers = new Mock<IProviderService>();
        providers.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(providerEntity);
        var handler = new UpdateAppointmentCommandHandler(
            Mock.Of<IMediator>(), providers.Object, Mock.Of<IBookingService>(), Mock.Of<IEventStore>());

        var result = await handler.Handle(new UpdateAppointmentCommand { AppointmentEntity = MakeAppointment() }, CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new UpdateAppointmentCommandHandler(
            Mock.Of<IMediator>(), Mock.Of<IProviderService>(), Mock.Of<IBookingService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
