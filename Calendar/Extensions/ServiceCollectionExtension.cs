using Library.Configuration;

namespace Calendar.Extensions;

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
        var appointmentsCollection = MongoConnectionResolver.ResolveSetting(configuration, "AppointmentsCollection", "appointments");
        var customersCollection = MongoConnectionResolver.ResolveSetting(configuration, "CustomersCollection", "customers");
        serviceCollection.AddScoped<IRepository<ProviderEntity>>(serviceProvider =>
            new MongoDbRepository<ProviderEntity>(
                serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(databaseName),
                providersCollection));

        serviceCollection.AddScoped<IRepository<AppointmentEntity>>(serviceProvider =>
            new MongoDbRepository<AppointmentEntity>(
                serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(databaseName),
                appointmentsCollection));

        serviceCollection.AddScoped<IRepository<CustomerEntity>>(serviceProvider =>
            new MongoDbRepository<CustomerEntity>(
                serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(databaseName),
                customersCollection));

        serviceCollection.AddScoped<ProviderService>();
        serviceCollection.AddScoped<CalendarService>();
        serviceCollection.AddScoped<CustomerService>();

        return serviceCollection;
    }
}
