namespace Kafka.Producer;

public interface IKafkaProducer
{
    Task<string> ProducerAsync(string topic, object message);
}