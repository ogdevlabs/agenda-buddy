namespace Identity.Extensions;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public static class ServiceCollectionExtension
{
    public static IServiceCollection AddMongoDbRepository(this IServiceCollection services,
        IConfiguration configuration)
    {
        var client = new MongoDbConfiguration(configuration).MongoClient();
        var database = client.GetDatabase(configuration.GetSection("MongoDbSettings")["DatabaseName"]);

        services.AddScoped<IRepository<CredentialEntity>>(
            _ => new MongoDbRepository<CredentialEntity>(database,
                configuration.GetSection("MongoDbSettings")["CollectionName"]!));

        services.AddScoped<IRepository<DeviceTokenEntity>>(
            _ => new MongoDbRepository<DeviceTokenEntity>(database, "device_tokens"));

        return services;
    }
}
