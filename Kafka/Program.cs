using MediatR;

namespace Kafka;

public class Program
{
    private readonly Mediator _mediator;

    public Program(Mediator mediator)
    {
        _mediator = mediator;
    }

    static async Task Main(string[] args)
    {
        var kafkaClient = new KafkaClient();
    }
}