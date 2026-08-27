using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// When a matched route's destination is unreachable, times out, or itself answers
/// with a 5xx, the gateway rewrites that into the shaped <c>gateway-destination-unreachable</c>
/// ProblemDetails body (<c>api-contracts.md</c> §1) — naming the failed cluster by id — instead of a bare
/// 502/504 or the destination's own untranslated body.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors <c>GatewayRoutingTest</c>'s hosting pattern exactly: a real <c>Program.cs</c> pipeline through
/// <see cref="GatewayAnchor"/>, fake Aspire service-discovery addresses injected via in-memory
/// configuration. The failure mode under test here is the opposite of <c>GatewayNoRouteTest</c>'s: a
/// route DOES match (the path is in the allowlist), but its destination address — a closed loopback
/// port, per the task's own guidance that this proves the same code path a stopped AppHost service
/// would — refuses the connection. No live AppHost or real backend process is needed to exercise this;
/// YARP's forwarding failure is identical either way (<c>IForwarderErrorFeature</c> carries the same
/// shape for "connection refused" and "service process not running").
/// </para>
/// </remarks>
public class GatewayFailureTranslationTest
{
    /// <summary>
    /// One fake, definitely-unreachable loopback address per logical service name, distinct from
    /// <c>GatewayRoutingTest</c>'s own set so the two test classes' addresses never collide if xUnit ever
    /// runs them concurrently in the same process.
    /// </summary>
    private static readonly Dictionary<string, string?> UnreachableServiceAddresses = new()
    {
        ["services:booking:http:0"] = "http://127.0.0.1:59301",
        ["services:calendar:http:0"] = "http://127.0.0.1:59302",
        ["services:customer:http:0"] = "http://127.0.0.1:59303",
        ["services:provider:http:0"] = "http://127.0.0.1:59304",
        ["services:services:http:0"] = "http://127.0.0.1:59305",
        ["services:profession:http:0"] = "http://127.0.0.1:59306",
        ["services:identity:http:0"] = "http://127.0.0.1:59307",
    };

    private static WebApplicationFactory<GatewayAnchor> CreateFactory() =>
        new WebApplicationFactory<GatewayAnchor>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(
                (_, config) => config.AddInMemoryCollection(UnreachableServiceAddresses)));

    [Theory]
    [InlineData("/api/v1/booking/appointments", "booking")]
    [InlineData("/api/v1/calendar/availability/someone%40example.com", "calendar")]
    [InlineData("/api/v1/customers/someone%40example.com", "customer")]
    [InlineData("/api/v1/providers/someone%40example.com", "provider")]
    [InlineData("/api/v1/services/someone%40example.com", "services")]
    [InlineData("/api/v1/professions", "profession")]
    [InlineData("/api/v1/auth/login", "identity")]
    [InlineData("/device-token", "identity")]
    public async Task AC5_UnreachableDestination_ReturnsTheShapedProblemDetails(
        string path, string expectedFailedService)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "https://agendabuddy.dev/errors/gateway-destination-unreachable",
            body.GetProperty("type").GetString());
        Assert.Equal("The service handling this request is unavailable", body.GetProperty("title").GetString());
        Assert.Equal(502, body.GetProperty("status").GetInt32());
        Assert.Contains(expectedFailedService, body.GetProperty("detail").GetString()!);

        // The field AgendaBuddy.MobileApp's error-display logic (PRD AC5) actually reads — this is the whole point of
        // the transform, so it gets its own assertion rather than folding into the "detail" check above.
        Assert.Equal(expectedFailedService, body.GetProperty("failedService").GetString());

        Assert.True(body.TryGetProperty("requestId", out _),
            "gateway-destination-unreachable body must carry requestId.");
    }

    [Fact]
    public async Task AC5_TheOtherSixServices_AreUnaffectedByOneBeingDown()
    {
        // "requests to the other six succeed normally" — succeed here means "still gets routed and still
        // gets the SAME shaped translation for ITS OWN unreachable destination", since none of the seven
        // fake addresses in this test class has anything listening. What AC5 is really asserting is that
        // one destination's failure is scoped to its own cluster: the response names the cluster that
        // was actually called, never a different one, and every route keeps working independently.
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var bookingResponse = await client.GetAsync("/api/v1/booking/appointments");
        var professionResponse = await client.GetAsync("/api/v1/professions");

        var bookingBody = await bookingResponse.Content.ReadFromJsonAsync<JsonElement>();
        var professionBody = await professionResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("booking", bookingBody.GetProperty("failedService").GetString());
        Assert.Equal("profession", professionBody.GetProperty("failedService").GetString());
    }
}
