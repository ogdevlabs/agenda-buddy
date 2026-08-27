namespace AgendaBuddy.EventAndCommands.Commands.Provider;

public class DeactivateProviderCommandHandler(
    IMediator mediator,
    ProviderService providerService,
    IEventStore eventStore)
    : IRequestHandler<DeactivateProviderCommand, string>
{
    public async Task<string> Handle(DeactivateProviderCommand request, CancellationToken cancellationToken)
    {
        // F-014 fix. This was `mediator.Publish(request, …)` — publishing the COMMAND, which is an
        // IRequest<string> and not an INotification. It compiles because Publish has an object overload, and it
        // throws ArgumentException at runtime: "…does not implement INotification". So this handler could never
        // have completed, and nobody knew, because nothing had ever dispatched it — the defect and its
        // unreachability arrived together.
        //
        // DeactivateProviderEvent already existed for exactly this purpose and had no references at all. Every
        // other command handler in this project publishes its event (see BookingAppointmentCommandHandler), so
        // this was a copy-paste that lost one line.
        await mediator.Publish(
            new DeactivateProviderEvent { ProviderEntity = request.ProviderEntity }, cancellationToken);

        var filter = SupportTools<ProviderEntity>.FilterByEmail(request.ProviderEntity.Email);
        var provider = await providerService.FindProvidersAsync(filter);

        if (provider is null)
        {
            var failEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = "DeactivateProviderCommand",
                Data = JsonSerializer.Serialize(request.ProviderEntity)
            };
            await eventStore.SaveAsync(failEvent);
            return null!;
        }

        // F-014 requirement 20: a targeted $set rather than setting the flag on a loaded document and calling
        // UpdateProviderAsync, which is a whole-document ReplaceOneAsync — it would discard any appointment
        // booked between this handler's read and its write. That was never a real risk while nothing
        // dispatched this command; F-014 makes it reachable, so it becomes one.
        await providerService.SetActiveAsync(provider.Email, isActive: false);
        provider.IsActive = false;

        var successEvent = new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Success",
            Type = "DeactivateProviderCommand",
            // ⚠️ This serialises the WHOLE provider — including its embedded appointments and therefore its
            // customers' email addresses — into the `events` collection. Unchanged from how every other
            // command handler audits (ADR-027 kept command payloads while F-016-T18 reduced query payloads to
            // a result count), so F-014 does not diverge here. But F-014 is what makes this handler reachable,
            // so it is what makes the PII land. Recorded for F-024, which owns erasure.
            Data = JsonSerializer.Serialize(provider)
        };
        await eventStore.SaveAsync(successEvent);
        return provider.ToJson();
    }
}
