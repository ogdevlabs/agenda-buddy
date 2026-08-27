using AgendaBuddy.Library.Configuration;

namespace Profession.Extensions;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Profession's repositories against the shared <see cref="IMongoClient"/> and
    /// queues the profession seed to run after the host starts.
    /// </summary>
    /// <remarks>
    /// Seeding used to run here as <c>SeedDataAsync(database, configuration).Wait()</c> — a
    /// blocking database call during service registration, at a point where no
    /// <see cref="IServiceProvider"/> exists to resolve the shared client from. It now runs in
    /// <see cref="ProfessionSeedHostedService"/> after the host is built, which removes the
    /// sync-over-async block as well.
    /// </remarks>
    public static IServiceCollection AddMongoDbRepository(this IServiceCollection serviceCollection,
        IConfiguration configuration)
    {
        var databaseName = MongoConnectionResolver.ResolveSetting(configuration, "DatabaseName", "agenda_buddy");
        var professionsCollection = MongoConnectionResolver.ResolveSetting(configuration, "ProfessionsCollection", "professions");
        var providersCollection = MongoConnectionResolver.ResolveSetting(configuration, "ProvidersCollection", "providers");

        serviceCollection.AddScoped<IRepository<ProfessionEntity>>(serviceProvider =>
            new MongoDbRepository<ProfessionEntity>(
                serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(databaseName),
                professionsCollection));

        serviceCollection.AddScoped<IRepository<ProviderEntity>>(serviceProvider =>
            new MongoDbRepository<ProviderEntity>(
                serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(databaseName),
                providersCollection));

        serviceCollection.AddScoped<ProfessionService>();
        serviceCollection.AddScoped<ProviderService>();

        serviceCollection.AddHostedService(serviceProvider =>
            new ProfessionSeedHostedService(
                serviceProvider.GetRequiredService<IMongoClient>(),
                databaseName,
                professionsCollection,
                serviceProvider.GetRequiredService<ILogger<ProfessionSeedHostedService>>()));

        return serviceCollection;
    }
}
