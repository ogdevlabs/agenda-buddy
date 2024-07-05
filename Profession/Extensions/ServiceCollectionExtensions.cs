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

        SeedDataAsync(database, configuration).Wait();
        
        return serviceCollection;
    }

    private static async Task SeedDataAsync(IMongoDatabase mongoDatabase, IConfiguration configuration)
    {
        var collectionName = configuration.GetSection("MongoDB")["ProfessionsCollection"];
        var collection = mongoDatabase.GetCollection<ProfessionEntity>(collectionName);
        if (!await collection.Find(_ => true).AnyAsync())
        {
            await collection.InsertManyAsync(ProfessionSeedData.SeedData());
        }
    }
}