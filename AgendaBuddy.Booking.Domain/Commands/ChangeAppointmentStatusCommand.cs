namespace AgendaBuddy.Booking.Domain.Commands;

/// <summary>
/// Moves one appointment to a new status, through the transition rules on the entity.
/// </summary>
/// <remarks>
/// A dedicated command rather than a field on
/// <see cref="UpdateAppointmentCommand"/>, because status is the one part of an appointment the caller does
/// not own: the update path now preserves the stored status and this is the only way to change it. Two doors
/// to the same state, one of them unguarded, would not be a fix.
/// </remarks>
[ExcludeFromCodeCoverage]
public class ChangeAppointmentStatusCommand : IRequest<Result<AppointmentEntity>>
{
    /// <summary>The appointment's business identifier.</summary>
    public required string Identifier { get; set; }

    /// <summary>The state to move to. Only <c>Booked</c> and <c>Completed</c> are reachable.</summary>
    public required AppointmentStatus TargetStatus { get; set; }
}
