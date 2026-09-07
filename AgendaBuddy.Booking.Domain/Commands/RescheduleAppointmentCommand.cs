namespace AgendaBuddy.Booking.Domain.Commands;

/// <summary>
/// A PROVIDER moving a booked session outright — no proposal, no approval.
/// </summary>
/// <remarks>
/// The asymmetry with <see cref="RequestRescheduleCommand"/> is deliberate and is the product decision: the
/// calendar is the provider's, so they move sessions on it; a customer asks. What was missing before was not the
/// ability to change the times — <c>PUT /appointments/</c> already did that — but that the change was SILENT:
/// nothing told the customer, and no record of the previous time survived.
/// </remarks>
[ExcludeFromCodeCoverage]
public class RescheduleAppointmentCommand : IRequest<Result<AppointmentEntity>>
{
    public required string Identifier { get; set; }

    /// <summary>The new start, UTC — the instant the availability response offered, sent back unchanged.</summary>
    public required DateTime NewStartUtc { get; set; }

    /// <summary>Who is moving it. Must be the provider on the appointment.</summary>
    public required string RequestedByEmail { get; set; }
}
