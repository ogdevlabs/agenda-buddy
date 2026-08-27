namespace Booking.Domain.Commands;

/// <summary>
/// Threat T-202: <c>UnauthorizedAccessException</c> and <c>KeyNotFoundException</c> from the handler
/// both map to 403 in Booking.Api, deliberately indistinguishably — a caller cannot tell "someone
/// else's note" from "no such note".
/// </summary>
[ExcludeFromCodeCoverage]
public class UpdateAppointmentNoteCommand : IRequest<Result<NoteEntity>>
{
    public required string Id { get; set; }
    public required string ProviderEmail { get; set; }
    public required string Content { get; set; }
}
