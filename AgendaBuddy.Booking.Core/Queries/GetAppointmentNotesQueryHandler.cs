namespace AgendaBuddy.Booking.Core.Queries;

// CONSTITUTION §3. Found by EventStoreWriteGuardTest, not by review: Notes/Payment
// operations were never audited -- the gap existed all along but the guard's
// file-based scan couldn't see it while the logic lived inline in Program.cs, not in a handler file.
public class GetAppointmentNotesQueryHandler(INoteService notes, IEventStore eventStore)
    : IRequestHandler<GetAppointmentNotesQuery, Result<IEnumerable<NoteEntity>>>
{
    public async Task<Result<IEnumerable<NoteEntity>>> Handle(GetAppointmentNotesQuery request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var found = (await notes.GetByAppointmentAsync(request.ProviderEmail, request.Identifier)).ToList();
        await eventStore.SaveAsync(QueryAudit.Success(nameof(GetAppointmentNotesQuery), found.Count));
        return Result.Ok<IEnumerable<NoteEntity>>(found);
    }
}
