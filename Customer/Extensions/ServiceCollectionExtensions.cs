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
}