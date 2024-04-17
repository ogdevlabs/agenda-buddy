namespace Kafka.Producer;


public class KafkaService: IKafkaService
{
    public Task CreateTopicAsync(string topicName, int numPartitions, short replicationFactor)
    {
        throw new NotImplementedException();
    }
}