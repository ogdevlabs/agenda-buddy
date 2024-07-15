using Kafka.Consumer;
using KafkaFlow;
using Microsoft.Extensions.DependencyInjection;

namespace Kafka.Extensions;

public static class KafkaConsumerConfiguration
{
    public static void AddKafkaConsumers(this IServiceCollection services, string topicName)
    {
        services.AddKafka(kafka => kafka
                .UseConsoleLog()
                .AddCluster(cluster => cluster
                    .WithBrokers(new[] { "localhost:9092" })
                    .AddConsumer(consumer => consumer
                        .Topic($"provider-{topicName}")
                        .WithGroupId("provider-group")
                        .WithBufferSize(100)
                        .WithWorkersCount(1)
                        // .AddMiddlewares(
                        //     middlewares => middlewares
                        //         .AddDeserializer<ProtobufNetDeserializer>()
                        //         .AddTypedHandlers(h => h.AddHandler<PrintConsoleHandler>())
                        // )
                    )
                )
            );
    }
}