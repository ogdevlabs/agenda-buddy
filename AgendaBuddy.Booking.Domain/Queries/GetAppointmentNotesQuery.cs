namespace AgendaBuddy.Booking.Domain.Queries;

/// <summary>
/// Session notes for one appointment, scoped to the calling provider. Ownership/role checks
/// (threat T-201: the provider email comes from the CALLER'S TOKEN, never the request) stay in
/// AgendaBuddy.Booking.Api, before dispatch — this query carries only the already-authorized primitives.
/// </summary>
[ExcludeFromCodeCoverage]
public class GetAppointmentNotesQuery : IRequest<Result<IEnumerable<NoteEntity>>>
{
    public required string ProviderEmail { get; set; }
    public required string Identifier { get; set; }
}
