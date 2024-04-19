using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Kafka;

public class KafkaClient: IKafkaClient
{
    public async Task<string> CreateTopicIfNotExist(string topicName)
    {
        var config = new AdminClientConfig
        {
            BootstrapServers = "broker:9092"
        };

        using var adminClient = new AdminClientBuilder(config).Build();
        
        try
        {
            var topic = new TopicSpecification
            {
                Name = topicName,
                NumPartitions = 1,
                ReplicationFactor = 1
            };
            await adminClient.CreateTopicsAsync(new[] { topic }, new CreateTopicsOptions()
            {
                OperationTimeout = TimeSpan.FromSeconds(5),
                RequestTimeout = TimeSpan.FromSeconds(10)
            });

            return $"Topic '{topicName}' created successfully";
        }
        catch (CreateTopicsException e)
        {
            if (e.Results[0].Error.Code == ErrorCode.TopicAlreadyExists)
            {
                return $"Topic '{topicName}' already exists.";
            }

            throw new Exception($"An error occurred creating topic '{topicName}': {e.Results[0].Error.Reason}");
        }
    }
}