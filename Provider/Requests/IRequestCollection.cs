using MediatR;

namespace Provider.Requests;

public interface IRequestCollection
{
    public Task<string> CreateTopicNotification(IMediator mediator, string topicName);
}