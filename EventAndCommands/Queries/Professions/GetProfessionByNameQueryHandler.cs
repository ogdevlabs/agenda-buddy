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
            await eventStore.SaveAsync(QueryAudit.Success("GetProfessionByNameQuery", 1));
            return profession;
        }
        else
        {
            await eventStore.SaveAsync(QueryAudit.Failure("GetProfessionByNameQuery"));
            return null!;
        }
    }
}