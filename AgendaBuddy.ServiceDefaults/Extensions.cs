using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace AgendaBuddy.ServiceDefaults;

/// <summary>
/// Cross-cutting defaults shared by every Agenda Buddy service: telemetry, health checks,
/// service discovery, and HTTP resilience. Deliberately storage-agnostic — it takes no
/// dependency on <c>MongoDB.Driver</c>, so the pinned driver never constrains this project.
/// </summary>
public static class Extensions
{
    /// <summary>The tag marking a check as a liveness probe rather than a readiness probe.</summary>
    private const string LiveTag = "live";

    /// <summary>
    /// Registers OpenTelemetry (traces, metrics, logs) with OTLP export, default health checks,
    /// service discovery, and standard <see cref="HttpClient"/> resilience. Call immediately
    /// after <c>WebApplication.CreateBuilder</c>.
    /// </summary>
    /// <typeparam name="TBuilder">The host application builder type.</typeparam>
    /// <param name="builder">The builder to configure.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Retries, circuit breaker, and timeout for every client in the process.
            http.AddStandardResilienceHandler();

            // Lets a client address another service by its AppHost resource name.
            http.AddServiceDiscovery();
        });

        return builder;
    }

    /// <summary>
    /// Maps <c>/health</c> (readiness — runs every check) and <c>/alive</c> (liveness — the
    /// <c>live</c> tag only). Both are mapped unconditionally; see ARCHITECTURE.md §7 Security.
    /// </summary>
    /// <param name="app">The application to map the endpoints on.</param>
    /// <returns>The same application, so calls can be chained.</returns>
    /// <remarks>
    /// The separation is the point: <c>/alive</c> must stay healthy when MongoDB is down, or an
    /// orchestrator restarts a process that is running correctly and merely waiting on its
    /// database. <c>/health</c> must go unhealthy so the process stops receiving traffic.
    /// </remarks>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapHealthChecks("/health");

        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(LiveTag)
        });

        return app;
    }

    /// <summary>
    /// Wires tracing, metrics, and logging, exporting over OTLP when an endpoint is configured.
    /// </summary>
    private static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    /// <summary>
    /// Adds the OTLP exporter only when an endpoint is configured. The AppHost injects it; CI
    /// and standalone runs have no collector, and exporting into the void would only add noise.
    /// </summary>
    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var otlpConfigured =
            !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (otlpConfigured) builder.Services.AddOpenTelemetry().UseOtlpExporter();

        return builder;
    }

    /// <summary>
    /// Adds the baseline liveness check. Readiness checks that touch a dependency — MongoDB, for
    /// instance — are registered per service, since this project stays storage-agnostic.
    /// </summary>
    private static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: [LiveTag]);

        return builder;
    }
}
