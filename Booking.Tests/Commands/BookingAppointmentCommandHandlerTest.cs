namespace Booking.Tests.Commands;

// F-019-T04. BookingService/ProviderService are concrete, non-virtual classes (Library is unchanged
// per this feature's explicit scope), so Moq cannot mock the business-logic path without an
// out-of-scope Library change. The GuardClause null-check below is the genuinely new, cheaply
// testable behavior; the Result<T> success/failure mapping is verified end-to-end by the real
// AgendaBuddy.IntegrationTests suite (Contract/Persistence/Audit against a real Mongo container) --
// the PRD's own named regression net for this rewrite.
[TestSubject(typeof(BookingAppointmentCommandHandler))]
public class BookingAppointmentCommandHandlerTest
{
    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new BookingAppointmentCommandHandler(
            Mock.Of<IMediator>(), null, null!, null!, Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
