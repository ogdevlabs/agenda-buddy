using Library.Entities;
using Library.Services;
using MediatR;

namespace Provider.Requests;

public interface IRequestCollection
{
    public Task<string> AddProviderRequest(
        IMediator mediator, 
        ProviderService providerService,
        ProviderEntity providerEntity);
    
    public Task<string> UpdateProviderRequest(
        string email,
        IMediator mediator, 
        ProviderService providerService,
        ProviderEntity providerEntity);
}