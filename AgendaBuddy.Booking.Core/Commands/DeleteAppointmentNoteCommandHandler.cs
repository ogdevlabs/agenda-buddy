namespace AgendaBuddy.Booking.Core.Commands;

// Same T-202 propagation as UpdateAppointmentNoteCommandHandler, and the same F-019-T05 /
// CONSTITUTION §3 audit-gap fix.
public class DeleteAppointmentNoteCommandHandler(INoteService notes, IEventStore eventStore)
    : IRequestHandler<DeleteAppointmentNoteCommand, Result>
{
    public async Task<Result> Handle(DeleteAppointmentNoteCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        await notes.DeleteAsync(request.Id, request.ProviderEmail);

        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Success",
            Type = nameof(DeleteAppointmentNoteCommand),
            // No entity to serialise -- it no longer exists. Id and the deleting provider only.
            Data = JsonSerializer.Serialize(new { request.Id, request.ProviderEmail })
        });

        return Result.Ok();
    }
}
