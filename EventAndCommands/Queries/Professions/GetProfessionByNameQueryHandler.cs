using EventAndCommands.Events.Profession;

namespace EventAndCommands.Queries.Professions;

public class GetProfessionByNameQueryHandler(IMediator mediator, ProfessionService professionService, string name, IEventStore eventStore)
    : IRequestHandler<GetProfessionByNameQuery, ProfessionEntity>
{

    public async Task<ProfessionEntity> Handle(GetProfessionByNameQuery request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new GetProfessionByNameEvent { Name = name }, cancellationToken);
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
            await eventStore.SaveAsync(successEvent);
            return profession;
        }
        else
        {
            var failEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = "GetProfessionByNameQuery",
                Data = JsonSerializer.Serialize("Not Found")
            };
            await eventStore.SaveAsync(failEvent);
            return null!;
        }
    }
}