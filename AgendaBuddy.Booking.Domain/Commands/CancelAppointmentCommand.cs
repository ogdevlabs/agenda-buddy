namespace AgendaBuddy.Booking.Domain.Commands;

[ExcludeFromCodeCoverage]
public class CancelAppointmentCommand : IRequest<Result<AppointmentEntity>>
{
    public required string Identifier { get; set; }

    /// <summary>
    /// Who is cancelling. Required, and it decides the rule that applies: a CUSTOMER must give
    /// <see cref="AppointmentEntity.CustomerCancellationNoticeHours"/> notice, a provider may cancel at any
    /// notice.
    /// </summary>
    /// <remarks>
    /// Also what lets the notification say who cancelled. Before this the command carried no such field, so both
    /// parties were told a session was cancelled with no indication of by whom — and the handler's own comment
    /// recorded that as a deliberate omission rather than inventing it.
    /// </remarks>
    public required string CancelledByEmail { get; set; }
}
