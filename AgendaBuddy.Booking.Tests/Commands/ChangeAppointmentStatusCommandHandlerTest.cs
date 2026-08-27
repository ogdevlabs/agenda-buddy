namespace AgendaBuddy.Booking.Tests.Commands;

// F-019-T05. Same rationale as BookingAppointmentCommandHandlerTest: BookingService/ProviderService
// are concrete, non-virtual (Library unchanged), so only the GuardClause path is unit-tested here;
// the real transition/persistence/audit logic is verified end-to-end by AgendaBuddy.IntegrationTests.
public class ChangeAppointmentStatusCommandHandlerTest
{
    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new ChangeAppointmentStatusCommandHandler(null!, null!, Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
