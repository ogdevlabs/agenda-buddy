using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace AgendaBuddy.ServiceDefaults.Tests;

/// <summary>
/// AC-13 and AC-14 at the middleware level: HSTS is emitted over TLS when the flag is on, never
/// over plain HTTP, and never at all when the flag is off.
/// </summary>
/// <remarks>
/// <para>
/// The same criteria are asserted again in <c>AgendaBuddy.IntegrationTests</c> against a real service
/// (AC-15), which is the version that counts — a control verified only against a hand-built pipeline is
/// verified against a pipeline no user ever meets. These run in the Docker-free unit gate, so a
/// regression is caught on every pull request rather than only when the container suite is run.
/// </para>
/// <para>
/// <b>The requests below use a non-<c>localhost</c> host on purpose.</b> ASP.NET's HSTS middleware skips
/// <c>localhost</c>, <c>127.0.0.1</c> and <c>[::1]</c> by default, which is a good default this project
/// keeps — a browser caches an HSTS directive stickily and across projects, so poisoning
/// <c>localhost</c> would break unrelated local work for weeks. A test that cleared the exclusion list
/// to make itself easier would be testing a configuration nothing ships.
/// </para>
/// </remarks>
[Collection(InProcessServerCollection.Name)]
public class TransportSecurityTest
{
    private const string HstsHeader = "Strict-Transport-Security";
    private const string ExternalHost = "agenda-buddy.example";

    private static async Task<WebApplication> StartServiceAsync(
        bool hstsEnabled, string environment = "Production")
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environment
        });

        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{TransportSecurityOptions.Section}:Enabled"] = hstsEnabled ? "true" : "false",
            [$"{TransportSecurityOptions.Section}:MaxAgeDays"] = "30"
        });

        builder.AddServiceDefaults();

        var app = builder.Build();
        app.UseAgendaBuddyTransportSecurity();
        app.MapGet("/probe", () => "ok");
        await app.StartAsync();

        return app;
    }

    private static async Task<HttpResponseMessage> GetAsync(WebApplication app, string scheme)
    {
        var client = app.GetTestServer().CreateClient();
        return await client.GetAsync($"{scheme}://{ExternalHost}/probe");
    }

    [Fact]
    public async Task T103_WhenEnabled_ResponsesOverTlsCarryStrictTransportSecurity()
    {
        await using var app = await StartServiceAsync(hstsEnabled: true);

        var response = await GetAsync(app, "https");

        Assert.True(
            response.Headers.Contains(HstsHeader),
            $"No {HstsHeader} on an HTTPS response with the flag on. Received: "
            + string.Join(", ", response.Headers.Select(header => header.Key)));
        Assert.Contains("max-age=2592000", response.Headers.GetValues(HstsHeader).Single());
    }

    [Fact]
    public async Task WhenEnabled_TheHeaderIsNotSentOverPlainHttp()
    {
        // AC-13's second half. Sending it over plaintext would be advice a man in the middle can strip,
        // and the framework declines to do it — asserted rather than assumed, because it is the property
        // that keeps a local HTTP run from being affected by the flag at all.
        await using var app = await StartServiceAsync(hstsEnabled: true);

        var response = await GetAsync(app, "http");

        Assert.DoesNotContain(HstsHeader, response.Headers.Select(header => header.Key));
    }

    [Fact]
    public async Task WhenDisabled_NoHstsHeaderIsSentEvenOverTls()
    {
        // AC-14: the AppHost's default configuration leaves both controls off, so a developer's stack
        // behaves exactly as it did without these controls.
        await using var app = await StartServiceAsync(hstsEnabled: false);

        var response = await GetAsync(app, "https");

        Assert.DoesNotContain(HstsHeader, response.Headers.Select(header => header.Key));
    }

    [Fact]
    public async Task TheServiceStillServesRequests_WhenTheControlsAreOff()
    {
        // The revert path: with both flags off nothing about request handling changes. Worth an
        // assertion because "off" is the shipped default, so this is the configuration almost every run
        // uses.
        await using var app = await StartServiceAsync(hstsEnabled: false);

        var response = await GetAsync(app, "http");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", await response.Content.ReadAsStringAsync());
    }

    // ── SecurityFlags: the startup audit (design decision D-7) ─────────────────────

    private static IConfiguration ConfigurationWith(params (string Key, string Value)[] entries) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(entries.ToDictionary(
                entry => entry.Key, entry => (string?)entry.Value))
            .Build();

    private sealed class StubEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    [Fact]
    public void T103_ADeploymentWithTheFlagsUnset_IsWarnedAbout()
    {
        // The threat is not an attack, it is a latent absence: a deployment that never sets the keys
        // ships with no throttling and no HSTS while the PRD, the episode and the roadmap all record the
        // feature as delivered. Same shape as a prior defect, where AssertRole was present in
        // the codebase and never called.
        var warnings = SecurityFlags.DisabledControls(
            ConfigurationWith(), new StubEnvironment("Production"), includeRateLimiting: true);

        Assert.Equal(2, warnings.Count);
        Assert.Contains(warnings, warning => warning.Contains("HSTS", StringComparison.Ordinal));
        Assert.Contains(warnings, warning => warning.Contains("Rate limiting", StringComparison.Ordinal));
    }

    [Fact]
    public void ALocalAppHostRun_IsNotWarnedAbout()
    {
        // D-6/D-7's whole point. Services run as PRODUCTION under the local AppHost, so the environment
        // name cannot carry "this is a laptop" — the AppHost injects a marker instead.
        var warnings = SecurityFlags.DisabledControls(
            ConfigurationWith((SecurityFlags.LocalRunKey, "true")),
            new StubEnvironment("Production"),
            includeRateLimiting: true);

        Assert.Empty(warnings);
    }

    [Fact]
    public void AStandaloneDevelopmentRun_IsNotWarnedAbout()
    {
        // `scripts/generate-openapi.sh` and a bare `dotnet run` both land here.
        Assert.Empty(SecurityFlags.DisabledControls(
            ConfigurationWith(), new StubEnvironment("Development"), includeRateLimiting: true));
    }

    [Fact]
    public void ADeploymentWithBothFlagsOn_IsNotWarnedAbout()
    {
        var warnings = SecurityFlags.DisabledControls(
            ConfigurationWith(
                ($"{TransportSecurityOptions.Section}:Enabled", "true"),
                ("Security:RateLimiting:Enabled", "true")),
            new StubEnvironment("Production"),
            includeRateLimiting: true);

        Assert.Empty(warnings);
    }

    [Fact]
    public void ServicesOtherThanIdentity_AreNotWarnedAboutTheLimiter()
    {
        // Booking never had a limiter and never spends BCrypt. Warning about it there would train
        // everyone to ignore the warning that matters.
        var warnings = SecurityFlags.DisabledControls(
            ConfigurationWith(), new StubEnvironment("Production"));

        Assert.Single(warnings);
        Assert.Contains("HSTS", warnings[0], StringComparison.Ordinal);
    }
}
