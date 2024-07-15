namespace Customer.Extensions;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMongoDbRepository(this IServiceCollection serviceCollection,
        IConfiguration configuration)
    {
        var client = new MongoDbConfiguration(configuration).MongoClient();
        var database = client.GetDatabase(configuration.GetSection("MongoDB")["DatabaseName"]);

        serviceCollection.AddScoped<IRepository<ProviderEntity>>(
            _ => new MongoDbRepository<ProviderEntity>(database,
                configuration.GetSection("MongoDB")["ProvidersCollection"]!));
        serviceCollection.AddScoped<IRepository<CustomerEntity>>(
            _ => new MongoDbRepository<CustomerEntity>(database,
                configuration.GetSection("MongoDB")["CustomersCollection"]!));

        serviceCollection.AddScoped<ProviderService>();
        serviceCollection.AddScoped<CustomerService>();

        return serviceCollection;
    }

    public static IServiceCollection AddKafkaBootstrap(this IServiceCollection serviceCollection,
        IConfiguration configuration)
    {
        serviceCollection.AddSingleton<KafkaProducer>(sp => new KafkaProducer(configuration.GetSection("Kafka")["BootstrapServers"]!));
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