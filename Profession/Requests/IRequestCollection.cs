namespace Profession.Requests;

public interface IRequestCollection
{
    public Task<ProfessionEntity> AddProfessionRequest(IMediator mediator, ProfessionService professionService,
        ProfessionEntity professionEntity);

    public Task<IEnumerable<ProfessionEntity>> GetProfessionsRequest(IMediator mediator,
        ProfessionService professionService);

    public Task<ProfessionEntity> GetProfessionByNameRequest(IMediator mediator, ProfessionService professionService,
        string name);
}