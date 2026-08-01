namespace Profession.Requests;

[ExcludeFromCodeCoverage]
public class RequestCollection(IEventStore eventStore) : IRequestCollection
{
    public async Task<ProfessionEntity> AddProfessionRequest(IMediator mediator, ProfessionService professionService,
        ProfessionEntity professionEntity)
    {
        var result = await new AddProfessionCommandHandler(
                mediator,
                professionService,
                professionEntity,
                eventStore)
            .Handle(
                new AddProfessionCommand() { ProfessionEntity = professionEntity }, new CancellationToken());
        return result;
    }

    public async Task<List<ProfessionEntity>> GetProfessionsRequest(IMediator mediator,
        ProfessionService professionService)
    {
        var result =
            await new GetProfessionsQueryHandler(mediator, professionService, eventStore).Handle(new GetProfessionsQuery(),
                new CancellationToken());
        return result;
    }

    public async Task<ProfessionEntity> GetProfessionByNameRequest(IMediator mediator, ProfessionService professionService,
        string name)
    {
        var result =
            await new GetProfessionByNameQueryHandler(mediator, professionService, name, eventStore).Handle(
                new GetProfessionByNameQuery { Name = name }, new CancellationToken());
        return result;
    }
}
