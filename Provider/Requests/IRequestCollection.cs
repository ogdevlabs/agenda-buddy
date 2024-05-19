using MediatR;

namespace Provider.Requests;

public interface IRequestCollection
{
    public Task<string> AddProviderRequest(IMediator mediator, string topicName);
}