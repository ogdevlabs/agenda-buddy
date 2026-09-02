using AgendaBuddy.Library.Configuration;

namespace AgendaBuddy.Profession.Extensions;

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

        // GetProfessionsQueryHandler/GetProfessionByNameQueryHandler are typed against
        // IProfessionService, not the concrete class -- it already covers everything they call.
        // Forwarding to the already-scoped concrete instance, not a second AddScoped<IProfessionService,
        // ProfessionService>, so a request that resolves both the concrete class and the interface in
        // the same scope gets the same object, not two.
        serviceCollection.AddScoped<IProfessionService>(sp => sp.GetRequiredService<ProfessionService>());

        // Same forwarding for IProviderService (2026-08-28, provider-professions handlers) --
        // ProviderService was registered above but never bound to its interface, so any handler
        // typed against IProviderService (matching every other service's own convention) failed
        // DI resolution with a 500 the moment it was actually dispatched through.
        serviceCollection.AddScoped<IProviderService>(sp => sp.GetRequiredService<ProviderService>());

        serviceCollection.AddHostedService(serviceProvider =>
            new ProfessionSeedHostedService(
                serviceProvider.GetRequiredService<IMongoClient>(),
                databaseName,
                professionsCollection,
                serviceProvider.GetRequiredService<ILogger<ProfessionSeedHostedService>>()));

        return serviceCollection;
    }
}
