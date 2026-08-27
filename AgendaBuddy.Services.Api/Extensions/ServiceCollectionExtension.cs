using AgendaBuddy.Library.Configuration;

namespace AgendaBuddy.Services.Extensions;

public static class ServiceCollectionExtension
{
    /// <summary>
    /// Registers the repositories for this service against the shared <see cref="IMongoClient"/>.
    /// </summary>
    /// <remarks>
    /// The client is resolved from the provider per registration rather than constructed here, so
    /// this method no longer opens a connection pool of its own (AC-4.3) and no longer depends on
    /// a connection string being present in configuration (AC-4.1). Names come from
    /// <see cref="MongoConnectionResolver"/>, so the Aspire-injected shape and every legacy shape
    /// resolve identically (R-3).
    /// </remarks>
    public static IServiceCollection AddMongoDbRepository(this IServiceCollection serviceCollection,
        IConfiguration configuration)
    {
        var databaseName = MongoConnectionResolver.ResolveSetting(configuration, "DatabaseName", "agenda_buddy");
        var providersCollection = MongoConnectionResolver.ResolveSetting(configuration, "ProvidersCollection", "providers");
        var servicesCollection = MongoConnectionResolver.ResolveSetting(configuration, "ServicesCollection", "services");
        serviceCollection.AddScoped<IRepository<ProviderEntity>>(serviceProvider =>
            new MongoDbRepository<ProviderEntity>(
                serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(databaseName),
                providersCollection));

        serviceCollection.AddScoped<IRepository<ServiceEntity>>(serviceProvider =>
            new MongoDbRepository<ServiceEntity>(
                serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(databaseName),
                servicesCollection));

        serviceCollection.AddScoped<ProviderService>();
        serviceCollection.AddScoped<ServiceService>();

        // F-020-T10: GetServicesFromProviderQueryHandler/AddServicesToProviderCommandHandler/
        // UpdateServicesFromProviderCommandHandler are typed against IProviderService, not the
        // concrete class -- it already covers everything they call. Forwarding to the already-scoped
        // concrete instance, not a second AddScoped<IProviderService, ProviderService>, so a request
        // that resolves both the concrete class and the interface in the same scope gets the same
        // object, not two (same pattern as Booking's/Calendar's/Profession's own ServiceCollectionExtension
        // -- the exact DI-registration gap each of those tasks' Party Reviews caught).
        serviceCollection.AddScoped<IProviderService>(sp => sp.GetRequiredService<ProviderService>());

        return serviceCollection;
    }
}
