using MediatR;

namespace Profession.Requests;

public interface IRequestCollection
{
    public Task<ProfessionEntity> AddProfessionRequest(IMediator mediator, ProfessionService professionService,
        ProfessionEntity professionEntity);
}