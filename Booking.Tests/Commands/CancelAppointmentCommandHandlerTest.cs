namespace Booking.Tests.Commands;

// F-019-T04. See BookingAppointmentCommandHandlerTest's remarks on why the business-logic path isn't
// mocked here.
[TestSubject(typeof(CancelAppointmentCommandHandler))]
public class CancelAppointmentCommandHandlerTest
{
    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new CancelAppointmentCommandHandler(
            Mock.Of<IMediator>(), null, null!, null!, Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
