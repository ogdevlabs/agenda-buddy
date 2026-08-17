using Library.Configuration;

namespace Identity.Extensions;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public static class ServiceCollectionExtension
{
    /// <summary>
    /// Registers Identity's repositories against the shared <see cref="IMongoClient"/>.
    /// </summary>
    /// <remarks>
    /// Identity's configuration shape differs from the domain services — it reads
    /// <c>MongoDbSettings:CollectionName</c> where they read per-entity names — which is why
    /// <see cref="MongoConnectionResolver.ResolveSetting"/> takes the name per call rather than
    /// assuming one convention.
    /// </remarks>
    public static IServiceCollection AddMongoDbRepository(this IServiceCollection services,
        IConfiguration configuration)
    {
        var databaseName = MongoConnectionResolver.ResolveSetting(configuration, "DatabaseName", "IdentityDb");
        var collectionName = MongoConnectionResolver.ResolveSetting(configuration, "CollectionName", "credentials");

        services.AddScoped<IRepository<CredentialEntity>>(serviceProvider =>
            new MongoDbRepository<CredentialEntity>(
                serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(databaseName),
                collectionName));

        // Collection name unchanged: it was hardcoded before this refactor too.
        services.AddScoped<IRepository<DeviceTokenEntity>>(serviceProvider =>
            new MongoDbRepository<DeviceTokenEntity>(
                serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(databaseName),
                "device_tokens"));

        return services;
    }
}
