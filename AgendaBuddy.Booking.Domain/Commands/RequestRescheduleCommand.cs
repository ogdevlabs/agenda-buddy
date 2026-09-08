namespace AgendaBuddy.Booking.Domain.Commands;

/// <summary>
/// A CUSTOMER proposing a new time for a booked session. The appointment does not move.
/// </summary>
/// <remarks>
/// The proposal names a specific instant, chosen from the provider's real availability, so approving it is one
/// tap and the slot is already known to have been free when it was picked. It is re-checked at approval, because
/// availability at proposal time is not a reservation.
/// </remarks>
[ExcludeFromCodeCoverage]
public class RequestRescheduleCommand : IRequest<Result<AppointmentEntity>>
{
    public required string Identifier { get; set; }

    /// <summary>The proposed start, UTC — the instant the availability response offered, unchanged.</summary>
    public required DateTime ProposedStartUtc { get; set; }

    /// <summary>Who is proposing. Recorded so the other party knows who owes the answer.</summary>
    public required string RequestedByEmail { get; set; }
}

/// <summary>
/// Answering an outstanding proposal. <see cref="Approve"/> moves the session; declining leaves it alone.
/// </summary>
/// <remarks>
/// One command for both answers rather than two, because they share every guard — same appointment, same
/// authorisation, same "is there still a proposal to answer" check — and differ only in the write. Two commands
/// would be two places for the same four preconditions to drift apart.
/// </remarks>
[ExcludeFromCodeCoverage]
public class AnswerRescheduleCommand : IRequest<Result<AppointmentEntity>>
{
    public required string Identifier { get; set; }

    public required bool Approve { get; set; }

    /// <summary>Who is answering. Must be a party to the appointment, and not the one who proposed.</summary>
    public required string AnsweredByEmail { get; set; }
}
