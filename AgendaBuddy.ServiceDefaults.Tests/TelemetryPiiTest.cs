using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using Xunit;

namespace AgendaBuddy.ServiceDefaults.Tests;

/// <summary>
/// Threat T-004 — customer and provider email addresses must not leave the process inside exported
/// spans.
/// </summary>
/// <remarks>
/// This was originally recorded as "reasoned, not observed" and deferred to a manual AppHost run.
/// That was a false constraint twice over: an in-memory exporter observes exactly what an OTLP
/// collector would receive with no container runtime, and the reasoning itself was wrong —
/// <c>http.route</c> is the template, but <c>url.path</c> carries the literal path, which in this
/// system contains email addresses.
/// <para>
/// Assertions read from the in-memory <b>exporter</b> rather than an <see cref="ActivityListener"/>,
/// so they see the tag set after <c>PiiRedactingProcessor</c> has run. A listener would race the
/// processor and could pass while real exports still leaked.
/// </para>
/// </remarks>
[Collection(InProcessServerCollection.Name)]
public class TelemetryPiiTest
{
    private const string Email = "customer.pii@example.com";
    private const string RouteTemplate = "/api/v1/providers/{email}";

    private static async Task<(WebApplication App, List<Activity> Exported)> StartServiceAsync()
    {
        var exported = new List<Activity>();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.AddServiceDefaults();

        // Reads what an OTLP collector would receive — after every processor.
        builder.Services.ConfigureOpenTelemetryTracerProvider(tracing =>
            tracing.AddInMemoryExporter(exported));

        var app = builder.Build();

        // Mirrors the real anonymous provider endpoints: PII in the path.
        app.MapGet(RouteTemplate, (string email) => Results.Ok(new { email }));

        await app.StartAsync();
        return (app, exported);
    }

    private static async Task<List<Activity>> RequestWithPiiAsync(string pathAndQuery)
    {
        var (app, exported) = await StartServiceAsync();
        await using (app)
        {
            var response = await app.GetTestServer().CreateClient().GetAsync(pathAndQuery);
            response.EnsureSuccessStatusCode();
        }

        // Disposing the app flushes the tracer provider, so every span has reached the exporter.
        return exported;
    }

    /// <summary>The span identifies the endpoint by template, which is what makes it aggregatable.</summary>
    [Fact]
    public async Task ExportedSpan_IdentifiesTheEndpointByRouteTemplate()
    {
        var exported = await RequestWithPiiAsync($"/api/v1/providers/{Uri.EscapeDataString(Email)}");

        // Selected by route rather than Assert.Single: the tracer listens process-wide, so spans
        // from other test classes' in-process servers land in this exporter too.
        Assert.Contains(RouteTemplate, exported.Select(a => a.GetTagItem("http.route") as string));
    }

    /// <summary>
    /// The assertion that actually protects the collector: no exported tag may carry the address.
    /// One leaking tag is a PII export.
    /// </summary>
    [Fact]
    public async Task ExportedSpan_CarriesNoTagContainingTheEmail_InThePath()
    {
        var exported = await RequestWithPiiAsync($"/api/v1/providers/{Uri.EscapeDataString(Email)}");

        Assert.Empty(Offenders(exported));
    }

    /// <summary>A query string is the other way an address reaches a URL tag.</summary>
    [Fact]
    public async Task ExportedSpan_CarriesNoTagContainingTheEmail_InTheQueryString()
    {
        var exported = await RequestWithPiiAsync(
            $"/api/v1/providers/someone?email={Uri.EscapeDataString(Email)}");

        Assert.Empty(Offenders(exported));
    }

    /// <summary>Redaction must not blank the tag — the path shape stays debuggable.</summary>
    [Fact]
    public async Task RedactionPreservesThePathShape()
    {
        var exported = await RequestWithPiiAsync($"/api/v1/providers/{Uri.EscapeDataString(Email)}");

        // Selected by route, for the reason ExportedSpan_IdentifiesTheEndpointByRouteTemplate already
        // gives: the tracer listens process-wide, so spans from other test classes' in-process servers
        // land in this exporter too. Taking the first non-empty url.path was a latent order dependency —
        // it passed only while this was the sole class in the assembly that started a server and issued a
        // request. F-021 added a second (TransportSecurityTest), and this test began failing on some runs
        // and passing on others while asserting a path belonging to /probe.
        var path = exported
            .Where(activity => (activity.GetTagItem("http.route") as string) == RouteTemplate)
            .Select(activity => activity.GetTagItem("url.path") as string)
            .FirstOrDefault(value => !string.IsNullOrEmpty(value));

        Assert.NotNull(path);
        Assert.StartsWith("/api/v1/providers/", path);
        Assert.Contains("[redacted-email]", path);
    }

    private static List<string> Offenders(IEnumerable<Activity> exported) =>
        exported
            .SelectMany(activity => activity.TagObjects
                .Select(tag => (activity.DisplayName, tag.Key, Value: tag.Value?.ToString())))
            .Where(entry => entry.Value is not null
                            && (entry.Value.Contains(Email, StringComparison.OrdinalIgnoreCase)
                                || entry.Value.Contains("customer.pii", StringComparison.OrdinalIgnoreCase)))
            .Select(entry => $"{entry.DisplayName}.{entry.Key}={entry.Value}")
            .ToList();
}
