using AgendaBuddy.Library.Configuration;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Repositories;
using AgendaBuddy.Library.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace AgendaBuddy.Library.Extensions;

public static class NotificationDeliveryExtensions
{
    /// <summary>
    /// Registers everything a service needs to <b>send</b> a notification on every channel:
    /// <see cref="INotificationService"/> for the inbox row, <see cref="IEmailSender"/>,
    /// <see cref="IPushSender"/> and the device-token lookup push needs, and the
    /// <see cref="INotificationDispatcher"/> that fans out across them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One call rather than a dozen lines repeated per service, because the set is only correct as a set — a
    /// service that registered the dispatcher but not the device-token repository would fail at request time
    /// with a missing dependency, and one that registered the notification repository against the wrong
    /// database would write rows the inbox cannot see.
    /// </para>
    /// <para>
    /// The device-token repository is bound to <b>Identity's</b> database, because that is where
    /// <c>POST /device-token</c> writes. It is read-only from here. The mirror of the arrangement Identity
    /// already has in the other direction, where it writes notifications into the shared
    /// <c>agenda_buddy</c> database so the inbox they belong to can see them.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddNotificationDelivery(
        this IServiceCollection services, IConfiguration configuration)
    {
        var databaseName = MongoConnectionResolver.ResolveSetting(configuration, "DatabaseName", "agenda_buddy");
        var notificationsCollection =
            MongoConnectionResolver.ResolveSetting(configuration, "NotificationsCollection", "notifications");

        services.AddScoped<IRepository<NotificationEntity>>(serviceProvider =>
            new MongoDbRepository<NotificationEntity>(
                serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(databaseName),
                notificationsCollection));

        services.AddScoped<INotificationService, NotificationService>();

        var identityDatabaseName =
            MongoConnectionResolver.ResolveSetting(configuration, "IdentityDatabaseName", "IdentityDb");
        var deviceTokensCollection =
            MongoConnectionResolver.ResolveSetting(configuration, "DeviceTokensCollection", "device_tokens");

        services.AddScoped<IRepository<DeviceTokenEntity>>(serviceProvider =>
            new MongoDbRepository<DeviceTokenEntity>(
                serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(identityDatabaseName),
                deviceTokensCollection));

        services.AddScoped<IDeviceTokenService, DeviceTokenService>();

        services.AddEmailDelivery(configuration);
        services.AddPushDelivery(configuration);

        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

        return services;
    }

    /// <summary>
    /// Registers <see cref="IPushSender"/>. Unconditionally, like
    /// <see cref="EmailDeliveryExtensions.AddEmailDelivery"/>: with nothing configured it resolves to
    /// <see cref="UnconfiguredPushSender"/>, which logs and reports that it delivered nothing rather than
    /// throwing, so a local run needs no push provider and callers need no null check.
    /// </summary>
    /// <remarks>
    /// There is currently only one implementation. An FCM one belongs here, selected on
    /// <see cref="PushOptions.FirebaseProjectId"/> being present, the same way
    /// <c>PaymentGatewayFactory</c> selects Stripe on an API key being present (ADR-038).
    /// </remarks>
    public static IServiceCollection AddPushDelivery(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PushOptions>(configuration.GetSection(PushOptions.Section));
        services.AddScoped<IPushSender, UnconfiguredPushSender>();

        return services;
    }
}
