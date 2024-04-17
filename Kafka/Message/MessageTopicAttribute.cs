namespace Kafka.Message;

[AttributeUsage(AttributeTargets.Class)]
public class MessageTopicAttribute(string topic) : Attribute
{
    public string Topic { get; } = topic;
}