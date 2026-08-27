namespace Booking.Domain.Commands;

/// <summary>Same T-202 indistinguishability as <see cref="UpdateAppointmentNoteCommand"/>.</summary>
[ExcludeFromCodeCoverage]
public class DeleteAppointmentNoteCommand : IRequest<Result>
{
    public required string Id { get; set; }
    public required string ProviderEmail { get; set; }
}
