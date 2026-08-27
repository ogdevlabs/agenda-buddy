namespace AgendaBuddy.Booking.Core.Commands;

// Threat T-202: UnauthorizedAccessException/KeyNotFoundException from INoteService.UpdateAsync are
// deliberately NOT caught here -- AgendaBuddy.Booking.Api maps both to 403 indistinguishably, and catching them
// here to return a generic Result.Fail would lose that distinction from an ordinary failure. That
// also means those two paths are unaudited -- same as every other exception-propagation path in this
// project's handlers (e.g. ChangeAppointmentStatusCommandHandler's InvalidOperationException).
// F-019-T05 / CONSTITUTION §3: see GetAppointmentNotesQueryHandler's remarks on the audit gap.
public class UpdateAppointmentNoteCommandHandler(INoteService notes, IEventStore eventStore)
    : IRequestHandler<UpdateAppointmentNoteCommand, Result<NoteEntity>>
{
    public async Task<Result<NoteEntity>> Handle(UpdateAppointmentNoteCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var updated = await notes.UpdateAsync(request.Id, request.ProviderEmail, request.Content);

        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Success",
            Type = nameof(UpdateAppointmentNoteCommand),
            Data = JsonSerializer.Serialize(updated)
        });

        return Result.Ok(updated);
    }
}
