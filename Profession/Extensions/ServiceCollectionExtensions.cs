namespace Profession.Extensions;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMongoDbRepository(this IServiceCollection serviceCollection,
        IConfiguration configuration)
    {
        var client = new MongoDbConfiguration(configuration).MongoClient();
        var database = client.GetDatabase(configuration.GetSection("MongoDB")["DatabaseName"]);

        serviceCollection.AddScoped<IRepository<ProfessionEntity>>(
            _ => new MongoDbRepository<ProfessionEntity>(database,
                configuration.GetSection("MongoDB")["ProfessionsCollection"]!));
        
        serviceCollection.AddScoped<IRepository<ProviderEntity>>(
            _ => new MongoDbRepository<ProviderEntity>(database,
                configuration.GetSection("MongoDB")["ProvidersCollection"]!));
        

        serviceCollection.AddScoped<ProfessionService>();
        serviceCollection.AddScoped<ProviderService>();
        
        return serviceCollection;
    }
}