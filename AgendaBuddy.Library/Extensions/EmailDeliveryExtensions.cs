using AgendaBuddy.Library.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgendaBuddy.Library.Extensions;

public static class EmailDeliveryExtensions
{
    /// <summary>
    /// Registers <see cref="IEmailSender"/> backed by Resend, bound to the <c>Email</c> configuration
    /// section.
    /// </summary>
    /// <remarks>
    /// Registered unconditionally, including when no API key is present: <see cref="ResendEmailSender"/>
    /// degrades to a logged no-op rather than throwing, so a local run needs no mail provider and callers
    /// need no null check. Registering only when configured would instead make the difference show up as a
    /// missing dependency at request time.
    /// </remarks>
    public static IServiceCollection AddEmailDelivery(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.Section));

        // Named client so an outbound-email timeout cannot be confused with a service-to-service one, and so
        // the resilience defaults ServiceDefaults applies to service discovery do not retry a send.
        services.AddHttpClient(ResendEmailSender.HttpClientName,
            client => client.Timeout = TimeSpan.FromSeconds(10));

        services.AddScoped<IEmailSender, ResendEmailSender>();

        return services;
    }
}
