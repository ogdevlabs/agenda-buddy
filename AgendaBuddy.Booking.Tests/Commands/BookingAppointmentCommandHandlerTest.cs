namespace AgendaBuddy.Booking.Tests.Commands;

// BookingService/ProviderService are concrete, non-virtual classes, so Moq cannot mock the
// business-logic path without a Library change. The GuardClause null-check below is the genuinely new,
// cheaply testable behavior; the Result<T> success/failure mapping is verified end-to-end by the real
// AgendaBuddy.IntegrationTests suite (Contract/Persistence/Audit against a real Mongo container).
[TestSubject(typeof(BookingAppointmentCommandHandler))]
public class BookingAppointmentCommandHandlerTest
{
    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new BookingAppointmentCommandHandler(
            Mock.Of<IMediator>(), null!, null!, Mock.Of<IEventStore>(), null!, Mock.Of<INotificationService>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
