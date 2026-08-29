namespace AgendaBuddy.Profession.Core.Commands;

// Coarse guard (2026-08-28): blocks removing ANY profession while the provider has an active/future
// appointment, regardless of which profession that appointment is actually for. Appointments do not
// record which service (let alone which profession) they were booked for -- agenda-buddy-e87 -- so a
// per-profession check is not implementable yet. Revisit once that gap closes.
public class RemoveProfessionFromProviderCommandHandler(
    IMediator mediator,
    IProviderService providerService,
    IEventStore eventStore) : IRequestHandler<RemoveProfessionFromProviderCommand, Result<List<string>>>
{
    public const string ActiveAppointmentsErrorMessage = "Cannot remove a profession while you have active appointments.";

    private static readonly HashSet<AppointmentStatus> ActiveStatuses =
        [AppointmentStatus.Requested, AppointmentStatus.Booked, AppointmentStatus.Confirmed];

    public async Task<Result<List<string>>> Handle(RemoveProfessionFromProviderCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        await mediator.Publish(new RemoveProfessionFromProviderEvent
        {
            Email = request.Email,
            ProfessionName = request.ProfessionName
        }, cancellationToken);

        var filter = SupportTools<ProviderEntity>.FilterByEmail(request.Email);
        var existing = await providerService.FindProvidersAsync(filter);
        if (existing is null)
        {
            await eventStore.SaveAsync(new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = nameof(RemoveProfessionFromProviderCommand),
                Data = JsonSerializer.Serialize(request)
            });
            return Result.Fail<List<string>>($"No provider found with email {request.Email}");
        }

        if (existing.AppointmentEntities.Any(a => ActiveStatuses.Contains(a.AppointmentStatus)))
        {
            await eventStore.SaveAsync(new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = nameof(RemoveProfessionFromProviderCommand),
                Data = JsonSerializer.Serialize(request)
            });
            return Result.Fail<List<string>>(ActiveAppointmentsErrorMessage);
        }

        var provider = await providerService.RemoveProfessionAsync(request.Email, request.ProfessionName);
        if (provider is null)
        {
            await eventStore.SaveAsync(new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = nameof(RemoveProfessionFromProviderCommand),
                Data = JsonSerializer.Serialize(request)
            });
            return Result.Fail<List<string>>($"No provider found with email {request.Email}");
        }

        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Success",
            Type = nameof(RemoveProfessionFromProviderCommand),
            Data = JsonSerializer.Serialize(provider.Professions)
        });
        return Result.Ok(provider.Professions);
    }
}
