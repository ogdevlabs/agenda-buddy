using AgendaBuddy.Library.Configuration;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Repositories;
using AgendaBuddy.Library.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;

namespace AgendaBuddy.Library.Extensions;

public static class AccountErasureExtensions
{
    /// <summary>
    /// Registers <see cref="IAccountErasureService"/> and every repository it scrubs through.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One call rather than eight registrations repeated per service, for the reason
    /// <see cref="NotificationDeliveryExtensions.AddNotificationDelivery"/> is one call: the set is only correct
    /// as a set. A service that registered the erasure service but not, say, the payments repository would throw
    /// at request time; worse, a variant that took its repositories optionally would <b>silently skip</b> a
    /// collection and report a successful erasure that left the address standing.
    /// </para>
    /// <para>
    /// <c>TryAddScoped</c> throughout, because the two services that call this already register some of these
    /// repositories themselves — Customer registers customers, providers and messages; Provider registers
    /// providers. Re-registering would give one request two repository instances for the same collection, which
    /// is harmless here but is the shape their own DI comments exist to avoid.
    /// </para>
    /// <para>
    /// The device-token repository is bound to <b>Identity's</b> database, the same read-only reach across
    /// databases <c>AddNotificationDelivery</c> already makes — except that here it is a write, because an erased
    /// account must stop being addressable by push. Deleting the token is not merely tidiness: the token
    /// identifies a device that is still in somebody's hand.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddAccountErasure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var databaseName = MongoConnectionResolver.ResolveSetting(configuration, "DatabaseName", "agenda_buddy");

        services.TryAddCollection<CustomerEntity>(configuration, "CustomersCollection", "customers", databaseName);
        services.TryAddCollection<ProviderEntity>(configuration, "ProvidersCollection", "providers", databaseName);
        services.TryAddCollection<AppointmentEntity>(configuration, "AppointmentsCollection", "appointments", databaseName);
        services.TryAddCollection<MessageEntity>(configuration, "MessagesCollection", "messages", databaseName);
        services.TryAddCollection<NotificationEntity>(configuration, "NotificationsCollection", "notifications", databaseName);
        services.TryAddCollection<NoteEntity>(configuration, "NotesCollection", "notes", databaseName);
        services.TryAddCollection<PaymentEntity>(configuration, "PaymentsCollection", "payments", databaseName);

        var identityDatabaseName =
            MongoConnectionResolver.ResolveSetting(configuration, "IdentityDatabaseName", "IdentityDb");
        services.TryAddCollection<DeviceTokenEntity>(
            configuration, "DeviceTokensCollection", "device_tokens", identityDatabaseName);

        services.TryAddScoped<IAccountErasureService, AccountErasureService>();

        return services;
    }

    private static void TryAddCollection<TEntity>(
        this IServiceCollection services,
        IConfiguration configuration,
        string settingName,
        string defaultCollectionName,
        string databaseName)
        where TEntity : class
    {
        var collectionName = MongoConnectionResolver.ResolveSetting(configuration, settingName, defaultCollectionName);

        services.TryAddScoped<IRepository<TEntity>>(serviceProvider =>
            new MongoDbRepository<TEntity>(
                serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(databaseName),
                collectionName));
    }
}
