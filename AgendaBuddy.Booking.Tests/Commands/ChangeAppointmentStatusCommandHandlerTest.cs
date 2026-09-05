namespace AgendaBuddy.Booking.Tests.Commands;

// Same rationale as BookingAppointmentCommandHandlerTest: BookingService/ProviderService
// are concrete, non-virtual, so only the GuardClause path is unit-tested here;
// the real transition/persistence/audit logic is verified end-to-end by AgendaBuddy.IntegrationTests.
public class ChangeAppointmentStatusCommandHandlerTest
{
    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new ChangeAppointmentStatusCommandHandler(null!, null!, Mock.Of<IEventStore>(), Mock.Of<INotificationDispatcher>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
