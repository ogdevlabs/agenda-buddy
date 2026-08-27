namespace AgendaBuddy.Profession.Core.Queries;

// Constructor takes only DI-resolvable services; the per-request name comes from the query, not a
// constructor parameter -- a `string name` constructor argument would mean the handler could never be
// dispatched through a real mediator.Send. Typed against IProfessionService, not the concrete class --
// it already covers everything this handler calls.
public class GetProfessionByNameQueryHandler(
    IMediator mediator,
    IProfessionService professionService,
    IEventStore eventStore) : IRequestHandler<GetProfessionByNameQuery, Result<ProfessionEntity>>
{
    public async Task<Result<ProfessionEntity>> Handle(GetProfessionByNameQuery request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        await mediator.Publish(new GetProfessionByNameEvent { Name = request.Name }, cancellationToken);

        var profession = await professionService.GetProfessionAsync(request.Name);
        if (profession is null)
        {
            await eventStore.SaveAsync(QueryAudit.Failure(nameof(GetProfessionByNameQuery)));
            return Result.Fail<ProfessionEntity>("No profession found with this name.");
        }

        await eventStore.SaveAsync(QueryAudit.Success(nameof(GetProfessionByNameQuery), 1));
        return Result.Ok(profession);
    }
}
