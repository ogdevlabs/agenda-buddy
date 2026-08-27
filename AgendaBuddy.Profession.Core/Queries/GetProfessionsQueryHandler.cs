namespace AgendaBuddy.Profession.Core.Queries;

// F-020-T09: moved from AgendaBuddy.EventAndCommands.Queries.Professions, following Booking's and
// Calendar's precedent. Typed against IProfessionService, not the concrete class -- it already
// covers everything this handler calls.
public class GetProfessionsQueryHandler(
    IMediator mediator,
    IProfessionService professionService,
    IEventStore eventStore) : IRequestHandler<GetProfessionsQuery, Result<List<ProfessionEntity>>>
{
    public async Task<Result<List<ProfessionEntity>>> Handle(GetProfessionsQuery request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        await mediator.Publish(new GetProfessionsEvent(), cancellationToken);

        var professionList = await professionService.GetProfessionCollectionAsync();
        if (professionList.Count == 0)
        {
            await eventStore.SaveAsync(QueryAudit.Failure(nameof(GetProfessionsQuery)));
            return Result.Fail<List<ProfessionEntity>>("No professions found.");
        }

        await eventStore.SaveAsync(QueryAudit.Success(nameof(GetProfessionsQuery), professionList.Count));
        return Result.Ok(professionList);
    }
}
