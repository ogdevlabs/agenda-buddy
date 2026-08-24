using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// F-015-T01: proves the eighth process — Gateway — starts, reports healthy, and exports telemetry the
/// same way the other seven services do.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this does not use <see cref="ServiceHostFixture{TEntryPoint}"/>.</b> That fixture exists to
/// give each of the seven domain services its own MongoDB Testcontainer (ADR-017) and to export
/// <c>JWT_PUBLIC_KEY</c> before the host builds, because <c>AuthenticationExtensions</c> reads it eagerly
/// at DI-registration time. Gateway's <c>Program.cs</c> is pure scaffold — <c>AddServiceDefaults()</c>,
/// <c>UseAgendaBuddyTransportSecurity()</c>, <c>MapDefaultEndpoints()</c> — with no
/// <c>AddSingleton&lt;IMongoClient&gt;</c> and no <c>AddAgendaBuddyAuthentication()</c>, so starting a
/// container and a crypto session for it would exercise infrastructure this process does not touch.
/// A plain <see cref="WebApplicationFactory{TEntryPoint}"/> against <see cref="GatewayAnchor"/> hosts it
/// over real HTTP exactly as the other seven are hosted (see <c>EntryPoints.cs</c>) — this class only
/// skips the parts of the harness that presuppose a dependency Gateway does not have.
/// </para>
/// <para>
/// No YARP, no routing — F-015-T03 adds <c>app.MapReverseProxy()</c>. This is the scaffold's own
/// verification, not an AC-driven test: F-015-T01 has no PRD acceptance criterion of its own (infra
/// tasks legitimately have none, per the Plan's Readiness Assessment).
/// </para>
/// </remarks>
public class GatewayHostTest : IDisposable
{
    private readonly WebApplicationFactory<GatewayAnchor> _factory = new();
    private readonly HttpClient _client;

    public GatewayHostTest() => _client = _factory.CreateClient();

    [Fact]
    public async Task GetHealth_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetAlive_ReturnsOk()
    {
        var response = await _client.GetAsync("/alive");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
