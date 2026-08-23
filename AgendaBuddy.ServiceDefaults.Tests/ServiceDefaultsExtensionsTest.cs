using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace AgendaBuddy.ServiceDefaults.Tests;

[Collection(InProcessServerCollection.Name)]
public class ServiceDefaultsExtensionsTest
{
    /// <summary>
    /// Builds a service exactly the way a real one does — AddServiceDefaults immediately after
    /// CreateBuilder, MapDefaultEndpoints after Build — but over the in-memory test server.
    /// </summary>
    private static async Task<WebApplication> StartServiceAsync(
        Action<IHealthChecksBuilder>? addChecks = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.AddServiceDefaults();

        addChecks?.Invoke(builder.Services.AddHealthChecks());

        var app = builder.Build();
        app.MapDefaultEndpoints();
        await app.StartAsync();

        return app;
    }

    private static HttpClient ClientFor(WebApplication app) =>
        app.GetTestServer().CreateClient();

    // AC-3.5: telemetry is on by default, so no service has to opt in.
    [Fact]
    public void AddServiceDefaults_RegistersOpenTelemetryTracingAndMetrics()
    {
        var builder = WebApplication.CreateBuilder();

        builder.AddServiceDefaults();

        using var app = builder.Build();
        Assert.NotNull(app.Services.GetService<TracerProvider>());
        Assert.NotNull(app.Services.GetService<MeterProvider>());
    }

    // AC-3.1: every service gets a liveness check without adding one itself.
    [Fact]
    public async Task AddServiceDefaults_AddsALiveTaggedSelfCheck()
    {
        await using var app = await StartServiceAsync();

        var service = app.Services.GetRequiredService<HealthCheckService>();
        var report = await service.CheckHealthAsync(registration => registration.Tags.Contains("live"));

        Assert.Contains("self", report.Entries.Keys);
        Assert.Equal(HealthStatus.Healthy, report.Status);
    }

    [Fact]
    public async Task MapDefaultEndpoints_HealthReturnsOk_WhenEverythingPasses()
    {
        await using var app = await StartServiceAsync();

        var response = await ClientFor(app).GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MapDefaultEndpoints_AliveReturnsOk_WhenEverythingPasses()
    {
        await using var app = await StartServiceAsync();

        var response = await ClientFor(app).GetAsync("/alive");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // AC-3.2/3.3 + risk R-6: a failing readiness check must take /health out of rotation.
    [Fact]
    public async Task MapDefaultEndpoints_HealthReturnsServiceUnavailable_WhenAReadinessCheckFails()
    {
        await using var app = await StartServiceAsync(checks =>
            checks.AddCheck("database", () => HealthCheckResult.Unhealthy(), tags: ["ready"]));

        var response = await ClientFor(app).GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    // The separation that matters: the process is alive even though its database is not, so an
    // orchestrator must not restart it. This is the whole point of two endpoints.
    [Fact]
    public async Task MapDefaultEndpoints_AliveStaysOk_WhenAReadinessCheckFails()
    {
        await using var app = await StartServiceAsync(checks =>
            checks.AddCheck("database", () => HealthCheckResult.Unhealthy(), tags: ["ready"]));

        var response = await ClientFor(app).GetAsync("/alive");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Conversely, a genuinely dead process must fail liveness.
    [Fact]
    public async Task MapDefaultEndpoints_AliveReturnsServiceUnavailable_WhenALiveCheckFails()
    {
        await using var app = await StartServiceAsync(checks =>
            checks.AddCheck("process", () => HealthCheckResult.Unhealthy(), tags: ["live"]));

        var response = await ClientFor(app).GetAsync("/alive");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    // AC-3.5: service discovery is wired for every HttpClient, so services address each other
    // by resource name rather than by hardcoded port.
    [Fact]
    public void AddServiceDefaults_ConfiguresHttpClientDefaults()
    {
        var builder = WebApplication.CreateBuilder();

        builder.AddServiceDefaults();

        using var app = builder.Build();
        Assert.NotNull(app.Services.GetService<IHttpClientFactory>());
    }

    // The escape hatch from R-1 must not creep back in: ServiceDefaults stays storage-agnostic,
    // so the CVE-pinned MongoDB.Driver line is never coupled to the telemetry stack.
    [Fact]
    public void ServiceDefaults_DoesNotReferenceMongoDbDriver()
    {
        var referenced = typeof(Extensions).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name);

        Assert.DoesNotContain("MongoDB.Driver", referenced);
    }
}
