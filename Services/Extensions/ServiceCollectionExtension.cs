namespace Services.Extensions;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddMongoDbRepository(this IServiceCollection serviceCollection,
        IConfiguration configuration)
    {
        var client = new MongoDbConfiguration(configuration).MongoClient();
        var database = client.GetDatabase(configuration.GetSection("MongoDB")["DatabaseName"]);

        serviceCollection.AddScoped<IRepository<ProviderEntity>>(
            provider => new MongoDbRepository<ProviderEntity>(database,
                configuration.GetSection("MongoDB")["CollectionName"]!));

        serviceCollection.AddScoped<ProviderService>();

        return serviceCollection;
    }
}