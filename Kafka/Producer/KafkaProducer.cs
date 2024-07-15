using KafkaFlow;

namespace Kafka.Producer;

public class KafkaProducer : IKafkaProducer
{
    private readonly IProducer<Null, string> _producer;
  

    public KafkaProducer(string bootstrapServers)
    {
        
        var config = new ProducerConfig { BootstrapServers = bootstrapServers };
        _producer = new ProducerBuilder<Null, string>(config).Build();
    }

    public async Task<string> ProducerAsync(string topic, object message)
    {
        var jsonString = JsonSerializer.Serialize(message);
        var response = await _producer.ProduceAsync(topic, new Message<Null, string> { Value = jsonString });
        return response.Status.ToString();
    }

   
}