namespace KakfaService.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKakfaServices(this IServiceCollection serviceCollection,
        IConfiguration configuration)
    {
        serviceCollection.AddSingleton<IProducer<Null, string>>(sp =>
        {
            var getKafkaConfig = configuration.GetSection("Kafka")["BootstrapServers"]!;
            var config = new ProducerConfig { BootstrapServers = getKafkaConfig };
            return new ProducerBuilder<Null, string>(config).Build();
        });

        serviceCollection.AddSingleton<IAdminClient>(sp =>
        {
            var getKafkaConfig = configuration.GetSection("Kafka")["BootstrapServers"]!;
            var config = new ProducerConfig { BootstrapServers = getKafkaConfig };
            return new AdminClientBuilder(config).Build();
        });
        return serviceCollection;
    }
}