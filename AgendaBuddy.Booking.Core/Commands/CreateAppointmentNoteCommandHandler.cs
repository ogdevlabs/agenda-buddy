namespace AgendaBuddy.Booking.Core.Commands;

// CONSTITUTION §3. See GetAppointmentNotesQueryHandler's remarks on the audit gap this
// handler shape exposed.
public class CreateAppointmentNoteCommandHandler(INoteService notes, IEventStore eventStore)
    : IRequestHandler<CreateAppointmentNoteCommand, Result<NoteEntity>>
{
    public async Task<Result<NoteEntity>> Handle(CreateAppointmentNoteCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        // providerEmail from the caller's token, identifier from the path -- both supplied by
        // AgendaBuddy.Booking.Api, never echoed from a client-controlled field on this command.
        var created = await notes.CreateAsync(new NoteEntity
        {
            ProviderEmail = request.ProviderEmail,
            AppointmentIdentifier = request.Identifier,
            Content = request.Content
        });

        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Success",
            Type = nameof(CreateAppointmentNoteCommand),
            Data = JsonSerializer.Serialize(created)
        });

        return Result.Ok(created);
    }
}
