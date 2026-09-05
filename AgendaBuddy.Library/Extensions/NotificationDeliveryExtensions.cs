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
    /// Registers <see cref="IPushSender"/> — <see cref="FcmPushSender"/> when Firebase credentials are
    /// configured, <see cref="UnconfiguredPushSender"/> when they are not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registered unconditionally either way, like <see cref="EmailDeliveryExtensions.AddEmailDelivery"/>, so a
    /// local run needs no push provider and callers need no null check. Selected on configuration rather than
    /// on <c>IsProduction()</c>, the same way <c>PaymentGatewayFactory</c> selects Stripe on an API key being
    /// present (ADR-038 / ADR-033 — every service runs as Production under the local AppHost, so the
    /// environment name cannot tell a laptop from a deployment).
    /// </para>
    /// <para>
    /// <b>Singleton</b>, not scoped: <see cref="FcmPushSender"/> caches one OAuth2 access token across sends,
    /// and a scoped registration would mint a fresh one per request — which Google rate-limits.
    /// </para>
    /// <para>
    /// To enable push, set <c>Push:FirebaseProjectId</c> and <c>Push:ServiceAccountJson</c> (Firebase Console →
    /// Project settings → Service accounts → Generate new private key). The JSON is a credential, so it belongs
    /// in user secrets or an Aspire secret parameter, never in <c>appsettings.json</c>.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddPushDelivery(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PushOptions>(configuration.GetSection(PushOptions.Section));

        var options = configuration.GetSection(PushOptions.Section).Get<PushOptions>() ?? new PushOptions();

        if (!string.IsNullOrWhiteSpace(options.FirebaseProjectId)
            && !string.IsNullOrWhiteSpace(options.ServiceAccountJson))
        {
            // Named client so an outbound push timeout cannot be confused with a service-to-service one, and so
            // the resilience defaults ServiceDefaults applies to service discovery do not retry a send.
            services.AddHttpClient(FcmPushSender.HttpClientName,
                client => client.Timeout = TimeSpan.FromSeconds(10));

            services.AddSingleton<IPushSender, FcmPushSender>();
        }
        else
        {
            services.AddSingleton<IPushSender, UnconfiguredPushSender>();
        }

        return services;
    }
}
