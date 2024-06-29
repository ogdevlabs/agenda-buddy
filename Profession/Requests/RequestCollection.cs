using MediatR;

namespace Profession.Requests;

public class RequestCollection : IRequestCollection
{
    public async Task<ProfessionEntity> AddProfessionRequest(IMediator mediator, ProfessionService professionService, ProfessionEntity professionEntity)
    {
        throw new NotImplementedException();
    }
}