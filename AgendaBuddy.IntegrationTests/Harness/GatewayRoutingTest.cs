using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// F-015-T03: the YARP route table must be an explicit <c>api/v1/{service}/**</c> allowlist covering
/// all seven backend services (never a catch-all forward), and a path outside every configured prefix
/// must get the <c>gateway-no-route</c> 404 shape rather than a proxied response (threat T-302,
/// <c>threat-model.md</c>).
/// </summary>
/// <remarks>
/// Both test classes here host the real <see cref="global::Gateway.Program"/> pipeline through
/// <see cref="GatewayAnchor"/> (see <c>Harness/GatewayHostTest.cs</c> for why a plain
/// <c>WebApplicationFactory</c> works for Gateway, unlike the other seven services) and inject fake
/// Aspire service-discovery addresses via in-memory configuration — exactly the
/// <c>services:&lt;name&gt;:http:0</c> keys <c>AspireServiceDiscoveryProxyConfigProvider</c> reads —
/// so the route table is fully populated without needing a real AppHost or the seven backend processes
/// running. None of the fake addresses have anything listening, so a request that *does* match an
/// allowlisted route fails downstream (connection refused, surfaced as a non-404 by YARP) rather than
/// succeeding — which is exactly the signal that distinguishes "matched a route, destination
/// unreachable" from "no route matched at all". Destination-unreachable failure *translation* into a
/// shaped ProblemDetails body is F-015-T04's job, not this task's.
/// </remarks>
public class GatewayRoutingTest
{
    /// <summary>
    /// One fake, definitely-unreachable loopback address per logical service name — the same names
    /// <c>AppHostWiring.cs</c> registers each of the seven services under.
    /// </summary>
    private static readonly Dictionary<string, string?> FakeServiceAddresses = new()
    {
        ["services:booking:http:0"] = "http://127.0.0.1:59201",
        ["services:calendar:http:0"] = "http://127.0.0.1:59202",
        ["services:customer:http:0"] = "http://127.0.0.1:59203",
        ["services:provider:http:0"] = "http://127.0.0.1:59204",
        ["services:services:http:0"] = "http://127.0.0.1:59205",
        ["services:profession:http:0"] = "http://127.0.0.1:59206",
        ["services:identity:http:0"] = "http://127.0.0.1:59207",
    };

    private static WebApplicationFactory<GatewayAnchor> CreateFactory() =>
        new WebApplicationFactory<GatewayAnchor>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(FakeServiceAddresses)));

    // ── Route-table shape: confirms the table itself is correct and complete ─────────────────────
    //
    // Reads the actual IProxyConfigProvider singleton out of the built DI container — the same
    // provider YARP itself consumes — rather than re-implementing the expected table and comparing.

    [Theory]
    [InlineData("booking", "/api/v1/booking/{**catch-all}")]
    [InlineData("calendar", "/api/v1/calendar/{**catch-all}")]
    [InlineData("customer", "/api/v1/customers/{**catch-all}")] // plural — 01-api-surface.md: "/api/v1/customers"
    [InlineData("provider", "/api/v1/providers/{**catch-all}")] // plural — 01-api-surface.md: "/api/v1/providers"
    [InlineData("services", "/api/v1/services/{**catch-all}")]
    [InlineData("profession", "/api/v1/professions/{**catch-all}")] // plural — 01-api-surface.md: "/api/v1/professions"
    public void RouteTable_MapsEachDomainPrefixToItsOwnCluster(string clusterId, string expectedPath)
    {
        using var factory = CreateFactory();
        var config = factory.Services.GetRequiredService<IProxyConfigProvider>().GetConfig();

        // Filter by RouteId, not ClusterId: since the messages/notifications fix, "customer" is no
        // longer a single-route cluster (RouteId == ClusterId still holds for this specific route
        // for every service here, including the plain "customer" -> /api/v1/customers/** entry).
        var route = Assert.Single(config.Routes, r => r.RouteId == clusterId);
        Assert.Equal(expectedPath, route.Match.Path);
        Assert.Equal(clusterId, route.ClusterId);

        var cluster = Assert.Single(config.Clusters, c => c.ClusterId == clusterId);
        Assert.Equal(
            FakeServiceAddresses[$"services:{clusterId}:http:0"],
            Assert.Single(cluster.Destinations!.Values).Address);
    }

    // Found live at F-015-T14: messages/notifications are two new TOP-LEVEL route groups on Customer
    // (ADR-036), not children of /api/v1/customers/**, so no InlineData row above ever matched them —
    // MobileApp's Messaging/Notifications screens were unreachable through the gateway. Both share the
    // "customer" cluster (RouteTable_HasExactlySevenClusters_NoMoreNoFewer below still holds — this adds
    // routes to an existing cluster, not a new one).
    [Theory]
    [InlineData("customer-messages", "/api/v1/messages/{**catch-all}")]
    [InlineData("customer-notifications", "/api/v1/notifications/{**catch-all}")]
    public void RouteTable_MapsTopLevelCustomerGroupsToTheCustomerCluster(string routeId, string expectedPath)
    {
        using var factory = CreateFactory();
        var config = factory.Services.GetRequiredService<IProxyConfigProvider>().GetConfig();

        var route = Assert.Single(config.Routes, r => r.RouteId == routeId);
        Assert.Equal(expectedPath, route.Match.Path);
        Assert.Equal("customer", route.ClusterId);
    }

    [Fact]
    public void RouteTable_MapsBothIdentityPathsToTheIdentityCluster()
    {
        using var factory = CreateFactory();
        var config = factory.Services.GetRequiredService<IProxyConfigProvider>().GetConfig();

        var identityRoutes = config.Routes.Where(r => r.ClusterId == "identity").ToList();

        Assert.Contains(identityRoutes, r => r.Match.Path == "/api/v1/auth/{**catch-all}");
        Assert.Contains(identityRoutes, r => r.Match.Path == "/device-token");

        var cluster = Assert.Single(config.Clusters, c => c.ClusterId == "identity");
        Assert.Equal(
            FakeServiceAddresses["services:identity:http:0"],
            Assert.Single(cluster.Destinations!.Values).Address);
    }

    [Fact]
    public void RouteTable_HasExactlySevenClusters_NoMoreNoFewer()
    {
        using var factory = CreateFactory();
        var config = factory.Services.GetRequiredService<IProxyConfigProvider>().GetConfig();

        Assert.Equal(
            new[] { "booking", "calendar", "customer", "identity", "profession", "provider", "services" },
            config.Clusters.Select(c => c.ClusterId).OrderBy(id => id, StringComparer.Ordinal));
    }

    // ── Live HTTP: each allowlisted prefix is at least attempted to be proxied ──────────────────────
    //
    // No real backend is listening on any fake address, so a request that matches an allowlisted route
    // fails downstream rather than succeeding. That failure is exactly the signal that the request WAS
    // routed (never a 404) — the opposite of T302's "no match" case below. The shaped 502 body for an
    // unreachable destination is F-015-T04's responsibility, not this task's; only "not a 404" is
    // asserted here.

    [Theory]
    [InlineData("/api/v1/booking/appointments")]
    [InlineData("/api/v1/calendar/availability/someone%40example.com")]
    [InlineData("/api/v1/customers/someone%40example.com")]
    [InlineData("/api/v1/providers/someone%40example.com")]
    [InlineData("/api/v1/services/someone%40example.com")]
    [InlineData("/api/v1/professions")]
    [InlineData("/api/v1/auth/login")]
    [InlineData("/device-token")]
    [InlineData("/api/v1/messages")] // found unreachable live at F-015-T14 — regression guard
    [InlineData("/api/v1/notifications")] // found unreachable live at F-015-T14 — regression guard
    public async Task AllowlistedPrefix_IsRoutedNotRejected(string path)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }
}

/// <summary>
/// T-302 (threat-model.md): the gateway's route table is an explicit allowlist, never a catch-all
/// forward. A request to any path outside every configured <c>api/v1/{service}/**</c> prefix — the
/// canonical example being a probe at a backend's own bare <c>/health</c>, reached through the gateway
/// rather than directly — must get a 404 in the <c>gateway-no-route</c> shape (<c>api-contracts.md</c>
/// §1), not a proxied response.
/// </summary>
public class GatewayNoRouteTest
{
    private static readonly Dictionary<string, string?> FakeServiceAddresses = new()
    {
        ["services:booking:http:0"] = "http://127.0.0.1:59201",
        ["services:calendar:http:0"] = "http://127.0.0.1:59202",
        ["services:customer:http:0"] = "http://127.0.0.1:59203",
        ["services:provider:http:0"] = "http://127.0.0.1:59204",
        ["services:services:http:0"] = "http://127.0.0.1:59205",
        ["services:profession:http:0"] = "http://127.0.0.1:59206",
        ["services:identity:http:0"] = "http://127.0.0.1:59207",
    };

    private static WebApplicationFactory<GatewayAnchor> CreateFactory() =>
        new WebApplicationFactory<GatewayAnchor>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(FakeServiceAddresses)));

    [Theory]
    // A backend's own bare /health, reached through the gateway rather than the allowlisted
    // api/v1/booking/** prefix — exactly T-302's motivating example.
    [InlineData("/booking/health")]
    // A typo'd / never-configured service segment under the api/v1 convention itself.
    [InlineData("/api/v1/nonexistent/probe")]
    // A stale client build calling the pre-F-015 broken path (no api/v1 prefix at all).
    [InlineData("/booking")]
    public async Task T302_UnmappedPath_Returns404NotProxied(string path)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "https://agendabuddy.dev/errors/gateway-no-route", body.GetProperty("type").GetString());
        Assert.Equal("No backend service matches this path", body.GetProperty("title").GetString());
        Assert.Equal(404, body.GetProperty("status").GetInt32());
        Assert.Contains(path, body.GetProperty("detail").GetString());
        Assert.True(body.TryGetProperty("requestId", out _), "gateway-no-route body must carry requestId.");
    }
}
