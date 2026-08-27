namespace AgendaBuddy.Kafka;

public interface IKafkaClient
{
    public Task<string> CreateTopicIfNotExist(string topicName);
}
