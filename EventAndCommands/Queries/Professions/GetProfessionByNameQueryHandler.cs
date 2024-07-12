namespace EventAndCommands.Queries.Professions;

public class GetProfessionByNameQueryHandler(IMediator mediator, ProfessionService professionService, string name)
    : IRequestHandler<GetProfessionByNameQuery, ProfessionEntity>
{
    [InjectService] private IEventStore? EventStore { get; } = new EventStore();

    public async Task<ProfessionEntity> Handle(GetProfessionByNameQuery request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new GetProfessionByNameEvent{ Name = name }, cancellationToken);
        var profession = await professionService.GetProfessionAsync(name);
        if (profession != null)
        {
            var successEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "GetProfessionByNameQuery",
                Data = JsonSerializer.Serialize(profession)
            };
            await EventStore!.SaveAsync(successEvent);
            return await Task.FromResult(profession);
        }
        else
        {
            var successEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = "GetProfessionByNameQuery",
                Data = JsonSerializer.Serialize("Not Found")
            };
            await EventStore!.SaveAsync(successEvent);
            return null!;
        }
    }
}