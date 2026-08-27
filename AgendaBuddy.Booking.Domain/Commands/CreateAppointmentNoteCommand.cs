namespace AgendaBuddy.Booking.Domain.Commands;

[ExcludeFromCodeCoverage]
public class CreateAppointmentNoteCommand : IRequest<Result<NoteEntity>>
{
    public required string ProviderEmail { get; set; }
    public required string Identifier { get; set; }
    public required string Content { get; set; }
}
