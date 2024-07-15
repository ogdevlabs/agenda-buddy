using Kafka.Producer;
using KafkaFlow;

namespace Kafka.Consumer;

public class KafkaConsumer : IMessageHandler<string>
{
    private readonly KafkaProducer _kafkaProducer;
    private readonly IConsumer<Null, string> _consumer;


    public async Task Handle(IMessageContext context, string message)
    {
        throw new NotImplementedException();
    }
}