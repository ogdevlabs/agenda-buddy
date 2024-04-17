using MediatR;

namespace Kafka.Message;

public class MessageNotification<TMessage>(TMessage message) : INotification
    where TMessage : IMessage
{
    public TMessage Message { get; } = message;
}