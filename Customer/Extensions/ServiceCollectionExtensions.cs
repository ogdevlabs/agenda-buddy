using Library.Configuration;

namespace Customer.Extensions;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
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
        var customersCollection = MongoConnectionResolver.ResolveSetting(configuration, "CustomersCollection", "customers");
        serviceCollection.AddScoped<IRepository<ProviderEntity>>(serviceProvider =>
            new MongoDbRepository<ProviderEntity>(
                serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(databaseName),
                providersCollection));

        serviceCollection.AddScoped<IRepository<CustomerEntity>>(serviceProvider =>
            new MongoDbRepository<CustomerEntity>(
                serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(databaseName),
                customersCollection));

        // F-014: messages and notifications. Both entities were written by F-006 and F-007 and have NEVER
        // been persisted, because nothing registered a repository for them. MongoDB creates each collection
        // on first write, so there is no migration.
        var messagesCollection = MongoConnectionResolver.ResolveSetting(configuration, "MessagesCollection", "messages");
        var notificationsCollection = MongoConnectionResolver.ResolveSetting(configuration, "NotificationsCollection", "notifications");

        serviceCollection.AddScoped<IRepository<MessageEntity>>(serviceProvider =>
            new MongoDbRepository<MessageEntity>(
                serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(databaseName),
                messagesCollection));

        serviceCollection.AddScoped<IRepository<NotificationEntity>>(serviceProvider =>
            new MongoDbRepository<NotificationEntity>(
                serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(databaseName),
                notificationsCollection));

        serviceCollection.AddScoped<ProviderService>();
        serviceCollection.AddScoped<CustomerService>();
        serviceCollection.AddScoped<IMessageService, MessageService>();
        serviceCollection.AddScoped<INotificationService, NotificationService>();

        return serviceCollection;
    }
}
