using AgendaBuddy.Library.Configuration;

namespace AgendaBuddy.Provider.Extensions;

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
        serviceCollection.AddScoped<IRepository<ProviderEntity>>(serviceProvider =>
            new MongoDbRepository<ProviderEntity>(
                serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(databaseName),
                providersCollection));

        serviceCollection.AddScoped<ProviderService>();

        // AddProviderCommandHandler/UpdateProviderCommandHandler/DeactivateProviderCommandHandler/
        // GetProviderByEmailQueryHandler/GetProvidersQueryHandler are typed against IProviderService, not the
        // concrete class -- forwarding to the already-scoped concrete instance, not a second
        // AddScoped<IProviderService, ProviderService>, so a request that resolves both the concrete class
        // and the interface in the same scope gets the same object, not two.
        serviceCollection.AddScoped<IProviderService>(sp => sp.GetRequiredService<ProviderService>());

        // Reporting. ReportingService reads the provider collection only — it needs no repository of
        // its own, which is part of why nobody noticed it was never registered.
        serviceCollection.AddScoped<IReportingService, ReportingService>();

        return serviceCollection;
    }
}
