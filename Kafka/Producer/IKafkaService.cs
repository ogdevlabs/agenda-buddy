namespace Kafka.Producer;

public interface IKafkaService
{
    Task CreateTopicAsync(string topicName, int numPartitions, short replicationFactor);
}