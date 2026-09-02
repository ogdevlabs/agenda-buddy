namespace AgendaBuddy.Profession.Core.Commands;

// Rejects any name that is not in the seeded catalog (Library/Data/ProfessionSeedData.cs) -- the only
// write this route needs to guard, since ADR-025 already retired POST /api/v1/professions itself; this
// command never adds to the catalog, only to a provider's own selection from it.
public class AddProfessionsToProviderCommandHandler(
    IMediator mediator,
    IProviderService providerService,
    IProfessionService professionService,
    IEventStore eventStore) : IRequestHandler<AddProfessionsToProviderCommand, Result<List<string>>>
{
    public async Task<Result<List<string>>> Handle(AddProfessionsToProviderCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        await mediator.Publish(new AddProfessionsToProviderEvent
        {
            Email = request.Email,
            ProfessionNames = request.ProfessionNames
        }, cancellationToken);

        var catalog = await professionService.GetProfessionCollectionAsync();
        var catalogNames = catalog.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = request.ProfessionNames.Where(n => !catalogNames.Contains(n)).ToList();
        if (unknown.Count > 0)
        {
            await eventStore.SaveAsync(new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = nameof(AddProfessionsToProviderCommand),
                Data = JsonSerializer.Serialize(request)
            });
            return Result.Fail<List<string>>($"Unknown profession(s): {string.Join(", ", unknown)}");
        }

        var provider = await providerService.AddProfessionsAsync(request.Email, request.ProfessionNames);
        if (provider is null)
        {
            await eventStore.SaveAsync(new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = nameof(AddProfessionsToProviderCommand),
                Data = JsonSerializer.Serialize(request)
            });
            return Result.Fail<List<string>>($"No provider found with email {request.Email}");
        }

        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Success",
            Type = nameof(AddProfessionsToProviderCommand),
            Data = JsonSerializer.Serialize(provider.Professions)
        });
        return Result.Ok(provider.Professions);
    }
}
