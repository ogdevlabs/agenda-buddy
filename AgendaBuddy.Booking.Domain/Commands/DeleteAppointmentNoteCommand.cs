namespace AgendaBuddy.Booking.Domain.Commands;

/// <summary>Same forbidden/not-found indistinguishability as <see cref="UpdateAppointmentNoteCommand"/>.</summary>
[ExcludeFromCodeCoverage]
public class DeleteAppointmentNoteCommand : IRequest<Result>
{
    public required string Id { get; set; }
    public required string ProviderEmail { get; set; }
}
