using AgendaBuddy.Library.Configuration;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Repositories;
using AgendaBuddy.Library.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

        var configured = !string.IsNullOrWhiteSpace(options.FirebaseProjectId)
                         && !string.IsNullOrWhiteSpace(options.ServiceAccountJson);

        // Only meaningful when something was supplied — "absent" and "unreadable" are different states and get
        // different messages.
        string? credentialProblem = null;
        var readable = configured
                       && FcmPushSender.CanReadServiceAccount(options.ServiceAccountJson!, out credentialProblem);

        if (readable)
        {
            // Named client so an outbound push timeout cannot be confused with a service-to-service one, and so
            // the resilience defaults ServiceDefaults applies to service discovery do not retry a send.
            services.AddHttpClient(FcmPushSender.HttpClientName,
                client => client.Timeout = TimeSpan.FromSeconds(10));

            services.AddSingleton<IPushSender, FcmPushSender>();
        }
        else
        {
            // ⚠️ A credential that is present but UNREADABLE degrades push to its documented off state; it does
            // not take the process's notification-dispatching routes with it.
            //
            // FcmPushSender is a lazily-resolved singleton whose constructor throws on a malformed credential, so
            // that throw landed on the first request needing a notification — and INotificationDispatcher is
            // injected into the Booking handlers as well as MessageModule, so appointment booking, cancellation,
            // status changes AND messaging all answered 502 over a credential only push reads. Push is
            // best-effort by contract (DispatchAsync never throws, every channel is independent); a channel that
            // cannot be constructed has to fail the same way a channel that cannot deliver does.
            if (credentialProblem is not null)
            {
                services.AddSingleton<IPushSender>(provider =>
                {
                    provider.GetService<ILoggerFactory>()
                        ?.CreateLogger(typeof(NotificationDeliveryExtensions))
                        .LogError(
                            "push.credential-unreadable: {Key} is set but could not be read ({Problem}), so push "
                            + "is disabled. Supply the service-account JSON raw, or base64-encoded.",
                            $"{PushOptions.Section}:{nameof(PushOptions.ServiceAccountJson)}",
                            credentialProblem);

                    return ActivatorUtilities.CreateInstance<UnconfiguredPushSender>(provider);
                });

                return services;
            }

            services.AddSingleton<IPushSender, UnconfiguredPushSender>();
        }

        return services;
    }
}
