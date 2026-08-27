using AgendaBuddy.Library.Configuration;

namespace AgendaBuddy.Identity.Extensions;

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

        // Deliberately against "agenda_buddy", not "IdentityDb": notifications are the
        // Customer/Provider-facing inbox (GET /api/v1/notifications), which reads that shared
        // database. Writing a password-reset notification into IdentityDb instead would make it
        // invisible to the very inbox it is meant to appear in.
        var notificationsDatabaseName =
            MongoConnectionResolver.ResolveSetting(configuration, "NotificationsDatabaseName", "agenda_buddy");
        var notificationsCollection =
            MongoConnectionResolver.ResolveSetting(configuration, "NotificationsCollection", "notifications");

        services.AddScoped<IRepository<NotificationEntity>>(serviceProvider =>
            new MongoDbRepository<NotificationEntity>(
                serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(notificationsDatabaseName),
                notificationsCollection));

        services.AddScoped<INotificationService, NotificationService>();

        return services;
    }
}
