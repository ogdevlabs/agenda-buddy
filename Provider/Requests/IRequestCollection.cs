using Library.Entities;
using Library.Services;
using MediatR;

namespace Provider.Requests;

public interface IRequestCollection
{
    // public Task<string> AddProviderRequest(IMediator mediator, string topicName);

    public Task<string> AddProviderRequest(
        IMediator mediator, 
        ProviderService providerService,
        ProviderEntity providerEntity);
}