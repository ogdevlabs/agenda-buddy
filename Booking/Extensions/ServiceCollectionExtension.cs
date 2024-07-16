using Confluent.Kafka;
using EventAndCommands.Messages;
using KafkaFlow;
using KafkaFlow.Serializer;
using Acks = KafkaFlow.Acks;

namespace Booking.Extensions;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtension
{
    public static IServiceCollection AddMongoDbRepository(this IServiceCollection serviceCollection,
        IConfiguration configuration)
    {
        var client = new MongoDbConfiguration(configuration).MongoClient();
        var database = client.GetDatabase(configuration.GetSection("MongoDB")["DatabaseName"]);

        serviceCollection.AddScoped<IRepository<ProviderEntity>>(
            _ => new MongoDbRepository<ProviderEntity>(database,
                configuration.GetSection("MongoDB")["ProvidersCollection"]!));

        serviceCollection.AddScoped<IRepository<AppointmentEntity>>(
            _ => new MongoDbRepository<AppointmentEntity>(database,
                configuration.GetSection("MongoDB")["AppointmentsCollection"]!));

        serviceCollection.AddScoped<IRepository<CustomerEntity>>(
            _ => new MongoDbRepository<CustomerEntity>(database,
                configuration.GetSection("MongoDB")["CustomersCollection"]!));

        serviceCollection.AddScoped<ProviderService>();
        serviceCollection.AddScoped<BookingService>();
        serviceCollection.AddScoped<CustomerService>();

        return serviceCollection;
    }
    
    public static IServiceCollection AddKafkaCustomerConfiguration(this IServiceCollection serviceCollection,
        IConfiguration configuration)
    {
        serviceCollection.AddKafka(kafka => kafka
            .UseConsoleLog()
            .AddCluster(cluster => cluster
                .WithBrokers(new[] { configuration.GetSection("Kafka")["BootstrapServers"] })
                .AddProducer("agenda-buddy-customer-topic", producer => producer
                    .DefaultTopic("agenda-buddy-provider-topic")
                    .WithAcks(Acks.All)
                    .AddMiddlewares(middlewares => middlewares
                        .AddSerializer<JsonCoreSerializer>()
                    )
                    .WithCompression(CompressionType.Gzip)
                    .WithLingerMs(5)
                )
                .AddConsumer(consumer => consumer
                    .Topic("agenda-buddy-provider-topic")
                    .WithGroupId("customer-group")
                    .WithBufferSize(100)
                    .WithWorkersCount(3)
                    .AddMiddlewares(middlewares => middlewares
                        .AddDeserializer<JsonCoreDeserializer>()
                        .AddTypedHandlers(handlers => handlers
                            .AddHandler<NotificationMessageHandler>()
                        )
                    )
                    .WithAutoCommitIntervalMs(5000)
                )
            )
        );
        return serviceCollection;
    }
}