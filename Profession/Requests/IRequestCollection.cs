namespace Profession.Requests;

public interface IRequestCollection
{
    public Task<List<ProfessionEntity>> GetProfessionsRequest(IMediator mediator,
        ProfessionService professionService);

    public Task<ProfessionEntity> GetProfessionByNameRequest(IMediator mediator, ProfessionService professionService,
        string name);
}
