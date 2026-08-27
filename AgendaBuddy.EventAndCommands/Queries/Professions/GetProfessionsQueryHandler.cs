using AgendaBuddy.EventAndCommands.Events.Profession;

namespace AgendaBuddy.EventAndCommands.Queries.Professions;

public class GetProfessionsQueryHandler(IMediator mediator, ProfessionService professionService, IEventStore eventStore)
    : IRequestHandler<GetProfessionsQuery, List<ProfessionEntity>>
{

    public async Task<List<ProfessionEntity>> Handle(GetProfessionsQuery request,
        CancellationToken cancellationToken)
    {
        await mediator.Publish(new GetProfessionsEvent(), cancellationToken);
        var professionList = await professionService.GetProfessionCollectionAsync();
        if (professionList.Count != 0)
        {
            await eventStore.SaveAsync(QueryAudit.Success("GetProfessionsQuery", professionList.Count));
            return professionList;
        }
        else
        {
            await eventStore.SaveAsync(QueryAudit.Failure("GetProfessionsQuery"));
            return null!;
        }
    }
}
