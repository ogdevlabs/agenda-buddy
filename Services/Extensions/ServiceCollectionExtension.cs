namespace Services.Extensions;

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

        serviceCollection.AddScoped<ProviderService>();
        serviceCollection.AddScoped<ServiceService>();

        return serviceCollection;
    }
}