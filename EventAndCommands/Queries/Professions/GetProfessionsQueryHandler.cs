using EventAndCommands.Events.Profession;

namespace EventAndCommands.Queries.Professions;

public class GetProfessionsQueryHandler(IMediator mediator, ProfessionService professionService)
    : IRequestHandler<GetProfessionsQuery, List<ProfessionEntity>>
{
    [InjectService] private IEventStore? EventStore { get; } = new EventStore();

    public async Task<List<ProfessionEntity>> Handle(GetProfessionsQuery request,
        CancellationToken cancellationToken)
    {
        await mediator.Publish(new GetProfessionsEvent(), cancellationToken);
        var professionCollection = await professionService.GetProfessionsAsync() ?? new List<ProfessionEntity>();
        var professsionList = professionCollection.ToList();
        if (professsionList.Count != 0)
        {
            var successEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "GetProfessionsQuery",
                Data = JsonSerializer.Serialize(professsionList)
            };
            await EventStore!.SaveAsync(successEvent);
            return await Task.FromResult(professsionList);
        }
        else
        {
            var successEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = "GetProfessionsQuery",
                Data = JsonSerializer.Serialize(professsionList)
            };
            await EventStore!.SaveAsync(successEvent);
            return null!;
        }
    }
}