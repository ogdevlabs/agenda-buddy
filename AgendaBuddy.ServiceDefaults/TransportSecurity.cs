using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgendaBuddy.ServiceDefaults;

/// <summary>
/// HSTS settings, bound from <c>Security:Hsts</c>.
/// </summary>
public sealed class TransportSecurityOptions
{
    /// <summary>The configuration section these bind from.</summary>
    public const string Section = "Security:Hsts";

    /// <summary>Emit <c>Strict-Transport-Security</c> on responses served over TLS.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// <c>max-age</c>, in days. Deliberately conservative: HSTS is hard to reverse, because a browser
    /// honours the directive for the whole window whatever the server later says.
    /// </summary>
    public int MaxAgeDays { get; set; } = 30;
}

/// <summary>
/// Whether these configuration-gated security controls are on, and whether their being off is
/// expected.
/// </summary>
/// <remarks>
/// <para>
/// <b>Configuration, not <c>IsProduction()</c>.</b> Services run as <b>Production</b> under the local
/// AppHost — <c>AppHostWiring.cs</c> adds each project with <c>launchProfileName: null</c> while
/// <c>launchSettings.json</c> sets <c>DOTNET_ENVIRONMENT=Development</c> for the AppHost process only
/// (verified: <c>/swagger/v1/swagger.json</c> 404s on all seven). So an environment-gated HSTS would
/// emit <c>Strict-Transport-Security</c> for <c>localhost</c>, which browsers cache stickily and across
/// projects, and an environment-gated limiter would throttle every local run (design decision D-6).
/// </para>
/// <para>
/// The price is a deployment that never sets the flags ships with the controls absent
/// while every artifact records the feature as delivered — the same shape as a prior defect,
/// where <c>AssertRole</c> existed and was never called. The mitigation is <b>warn loudly, do not fail
/// fast</b> (D-7): a config slip should be visible, not an outage.
/// </para>
/// </remarks>
public static class SecurityFlags
{
    /// <summary>
    /// Set by the AppHost for a local run, so a service can tell "off because this is a laptop" from
    /// "off because someone forgot".
    /// </summary>
    /// <remarks>
    /// A marker injected by the composition root rather than a guess from the environment name, because
    /// the environment name is exactly what cannot carry this distinction here. A standalone
    /// <c>dotnet run</c> is treated as local too, via <c>IsDevelopment()</c>.
    /// </remarks>
    public const string LocalRunKey = "Security:Local";

    /// <summary>Whether HSTS is switched on.</summary>
    public static bool HstsEnabled(IConfiguration configuration) =>
        configuration.GetValue($"{TransportSecurityOptions.Section}:Enabled", false);

    /// <summary>
    /// Whether per-IP rate limiting is switched on. Read from here rather than from Identity so the
    /// startup audit can name it without ServiceDefaults depending on Identity.
    /// </summary>
    public static bool RateLimitingEnabled(IConfiguration configuration) =>
        configuration.GetValue("Security:RateLimiting:Enabled", false);

    /// <summary>Whether this process is running on a developer's machine.</summary>
    public static bool IsLocalRun(IConfiguration configuration, IHostEnvironment environment) =>
        environment.IsDevelopment() || configuration.GetValue(LocalRunKey, false);

    /// <summary>
    /// One warning per security control that is off while this does not look like a local run.
    /// Empty on a local run, and empty when the controls are on.
    /// </summary>
    /// <param name="configuration">The service's configuration.</param>
    /// <param name="environment">The host environment.</param>
    /// <param name="includeRateLimiting">
    /// <c>true</c> only for Identity. The limiter protects the two routes that spend BCrypt, so warning
    /// about its absence in Booking would be noise about a control Booking never had.
    /// </param>
    public static IReadOnlyList<string> DisabledControls(
        IConfiguration configuration,
        IHostEnvironment environment,
        bool includeRateLimiting = false)
    {
        if (IsLocalRun(configuration, environment)) return [];

        var warnings = new List<string>();

        if (!HstsEnabled(configuration))
        {
            warnings.Add(
                $"HSTS is OFF: set {TransportSecurityOptions.Section}:Enabled=true. Responses will not "
                + "carry Strict-Transport-Security, so a client that reaches this service over plain "
                + "HTTP once will keep doing so.");
        }

        if (includeRateLimiting && !RateLimitingEnabled(configuration))
        {
            warnings.Add(
                "Rate limiting is OFF: set Security:RateLimiting:Enabled=true. login and register each "
                + "spend ~262 ms of CPU on BCrypt per request, so roughly 4 unauthenticated requests "
                + "per second pin a core.");
        }

        return warnings;
    }
}

/// <summary>
/// The transport-security half of the pipeline: HSTS under its flag, then HTTPS redirection.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is one policy plus one call per service rather than a single edit.</b> Middleware order
/// is a property of each <c>Program.cs</c> — it is the sequence of <c>app.UseX()</c> calls — and
/// <c>AddServiceDefaults()</c> runs on the <i>builder</i>, before any pipeline exists, so it cannot
/// reposition anything. ServiceDefaults therefore owns the policy and each service places the call.
/// </para>
/// </remarks>
public static class TransportSecurityExtensions
{
    /// <summary>
    /// Configures the HSTS policy. Called from <c>AddServiceDefaults</c>, so no service opts in.
    /// </summary>
    public static IServiceCollection AddAgendaBuddyTransportSecurity(
        this IServiceCollection services, IConfiguration configuration)
    {
        var maxAgeDays = configuration.GetValue($"{TransportSecurityOptions.Section}:MaxAgeDays", 30);

        services.AddHsts(hsts =>
        {
            hsts.MaxAge = TimeSpan.FromDays(maxAgeDays);

            // Neither by default, per ARCHITECTURE.md §8: both are the hard-to-reverse parts, and a
            // wrong preload submission outlives the mistake by months. A deployment that wants them
            // opts in deliberately.
            hsts.IncludeSubDomains = false;
            hsts.Preload = false;
        });

        return services;
    }

    /// <summary>
    /// Registers HSTS (when enabled) and HTTPS redirection. Call <b>immediately before</b>
    /// <c>UseAuthentication()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Placement is the point.</b> All seven services used to register <c>UseHttpsRedirection</c>
    /// <i>after</i> <c>UseAuthentication</c>, so the bearer token was parsed and validated out of a
    /// plaintext request before the redirect was issued (PRD requirement 13).
    /// </para>
    /// <para>
    /// Reordering does not fix what it appears to fix, which is why HSTS is here too: by the time any
    /// middleware runs, the password or token has <b>already crossed the wire</b>. Redirection protects
    /// nothing that has already been sent; HSTS is what stops the client making the same mistake next
    /// time.
    /// </para>
    /// <para>
    /// Redirection itself is <b>not</b> flag-gated. Six services already called it unconditionally, so a
    /// flag defaulting to off would silently remove an existing control, and one defaulting to on would
    /// be decorative. It is a no-op wherever no HTTPS port is known — which is why the integration
    /// harness and every local HTTP run are unaffected.
    /// </para>
    /// </remarks>
    /// <param name="app">The application whose pipeline is being built.</param>
    /// <param name="includeRateLimitingInAudit">
    /// Passed through to <see cref="SecurityFlags.DisabledControls"/>; <c>true</c> only for Identity.
    /// </param>
    public static WebApplication UseAgendaBuddyTransportSecurity(
        this WebApplication app, bool includeRateLimitingInAudit = false)
    {
        ArgumentNullException.ThrowIfNull(app);

        foreach (var warning in SecurityFlags.DisabledControls(
                     app.Configuration, app.Environment, includeRateLimitingInAudit))
        {
            // Warning, not a throw. A missing flag on a deployment should be loud and fixable, not an
            // outage — and it must not be able to stop a service that is otherwise healthy (D-7).
            app.Logger.LogWarning("SECURITY CONTROL DISABLED — {Warning}", warning);
        }

        if (SecurityFlags.HstsEnabled(app.Configuration))
        {
            // Writes the header only on responses served over TLS, and never for the excluded hosts
            // (localhost, 127.0.0.1, [::1]) the framework skips by default — so enabling this in a
            // local experiment cannot poison a browser's HSTS cache for localhost.
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        return app;
    }
}
