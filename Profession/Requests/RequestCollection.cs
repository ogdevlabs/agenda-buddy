namespace Profession.Requests;

[ExcludeFromCodeCoverage]
public class RequestCollection : IRequestCollection
{
    public async Task<ProfessionEntity> AddProfessionRequest(IMediator mediator, ProfessionService professionService,
        ProfessionEntity professionEntity)
    {
        var result = await new AddProfessionCommandHandler(
                mediator,
                professionService,
                professionEntity)
            .Handle(
                new AddProfessionCommand() { ProfessionEntity = professionEntity }, new CancellationToken());
        return result;
    }

    public async Task<IEnumerable<ProfessionEntity>> GetProfessionsRequest(IMediator mediator,
        ProfessionService professionService)
    {
        var result =
            await new GetProfessionsQueryHandler(mediator, professionService).Handle(new GetProfessionsQuery(),
                new CancellationToken());
        return result;
    }

    public async Task<ProfessionEntity> GetProfessionByNameRequest(IMediator mediator, ProfessionService professionService,
        string name)
    {
        var result =
            await new GetProfessionByNameQueryHandler(mediator, professionService, name).Handle(
                new GetProfessionByNameQuery { Name = name }, new CancellationToken());
        return result;
    }
}