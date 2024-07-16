namespace Provider.Extensions;

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

        serviceCollection.AddScoped<IRepository<ServiceEntity>>(
            _ => new MongoDbRepository<ServiceEntity>(database,
                configuration.GetSection("MongoDB")["ServicesCollection"]!));

        serviceCollection.AddScoped<IRepository<ProfessionEntity>>(
            _ => new MongoDbRepository<ProfessionEntity>(database,
                configuration.GetSection("MongoDB")["ProfessionsCollection"]!));

        serviceCollection.AddScoped<ProviderService>();
        serviceCollection.AddScoped<ServiceService>();
        serviceCollection.AddScoped<ProfessionService>();

        return serviceCollection;
    }

    public static IServiceCollection AddKafkaProviderConfiguration(this IServiceCollection serviceCollection,
        IConfiguration configuration)
    {
        serviceCollection.AddKafka(kafka => kafka
            .UseConsoleLog()
            .AddCluster(cluster => cluster
                .WithBrokers(new[] { configuration.GetSection("Kafka")["BootstrapServers"] })
                .AddProducer("agenda-buddy-provider-producer", producer => producer
                    .DefaultTopic("agenda-buddy-customer-topic")
                    .WithAcks(Acks.All)
                    .AddMiddlewares(middlewares => middlewares
                        .AddSerializer<JsonCoreSerializer>()
                    )
                    .WithCompression(CompressionType.Gzip)
                    .WithLingerMs(5)
                )
                .AddConsumer(consumer => consumer
                    .Topic("agenda-buddy-provider-topic")
                    .WithGroupId("provider-consumer-group")
                    .WithBufferSize(100)
                    .WithWorkersCount(4)
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

    public static IServiceCollection AddKakfaServices(this IServiceCollection serviceCollection,
        IConfiguration configuration)
    {
        serviceCollection.AddSingleton<IProducer<Null, string>>(sp =>
        {
            var getKafkaConfig = configuration.GetSection("Kafka")["BootstrapServers"]!;
            var config = new ProducerConfig { BootstrapServers = getKafkaConfig };
            return new ProducerBuilder<Null, string>(config).Build();
        });

        return serviceCollection;
    }
}