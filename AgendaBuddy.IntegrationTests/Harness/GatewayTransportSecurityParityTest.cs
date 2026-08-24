using System.Net;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// F-015-T04 <c>[security]</c> AC / threat T-303 (<c>threat-model.md</c>): a request reaching a backend
/// service <b>through the gateway</b> must not change that service's HSTS/redirect behaviour compared to
/// a direct call — no new redirect loop, no incorrect scheme in a <c>Location</c> header.
/// </summary>
/// <remarks>
/// <para>
/// <b>TDD note (test-first, per the task).</b> Written against the pre-existing Gateway/Profession code
/// with no production change made for it, and it passed on first run: T-303's own threat-model entry
/// already predicted this ("YARP's default HttpTransformer does forward X-Forwarded-* headers"), and
/// neither Gateway nor any of the seven backends calls <c>UseForwardedHeaders()</c>, so the
/// <c>X-Forwarded-Host</c>/<c>X-Forwarded-Proto</c> headers YARP adds by default are inert as far as
/// <c>UseAgendaBuddyTransportSecurity()</c> is concerned — that middleware reads
/// <c>HttpContext.Request.Scheme</c>/<c>Host</c> directly, which resolve from whatever scheme/host the
/// request actually arrived on, gateway hop or not. Because "no production change" leaves no natural
/// red/green pair, this was instead confirmed non-vacuous by mutation: temporarily adding
/// <c>app.UseForwardedHeaders(new() { ForwardedHeaders = ForwardedHeaders.All })</c> to Profession's
/// pipeline (simulating exactly what T-303 warns against) turned the <c>scheme: "http"</c> case of
/// <see cref="T303_GatewayForwardedRequest_TransportSecurityBehaviorMatchesDirectCall"/> red — the
/// gateway-forwarded call started reporting HSTS where the direct call did not, because the backend
/// started trusting the client's forwarded scheme instead of the scheme it actually received the
/// request on. Reverted immediately after confirming that failure; production code is unchanged. This
/// test exists so the fact it currently passes is asserted, not assumed — and so it starts failing again
/// the moment someone adds <c>UseForwardedHeaders()</c> to a backend for real.
/// </para>
/// <para>
/// Mirrors <c>TransportSecurityHeaderTest</c>'s direct-call assertions exactly, against Profession's
/// anonymous <c>api/v1/professions</c> route (no JWT needed, so nothing here can be confounded by an
/// auth failure) — the only addition is doing the same call a second way, through
/// <see cref="GatewayToRealServiceHarness"/>'s in-process bridge, and asserting parity rather than
/// re-deriving the expectation.
/// </para>
/// <para>
/// <b>Why the gateway-facing scheme is fixed and the destination scheme varies.</b> Neither service
/// calls <c>UseForwardedHeaders()</c>, so what Profession itself sees as its own request scheme comes
/// from YARP's outbound destination address (<see cref="GatewayToRealServiceHarness"/>'s
/// <c>destinationScheme</c>), not from what the client sent the gateway. Varying the client-facing
/// scheme here would test nothing extra; varying the destination scheme is what actually changes what
/// Profession sees, so that is the axis this test controls to match each direct-call comparison.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class GatewayTransportSecurityParityTest(ServiceHostFixture<ProfessionAnchor> host)
    : IClassFixture<ServiceHostFixture<ProfessionAnchor>>
{
    private const string HstsHeader = "Strict-Transport-Security";
    private const string ExternalHost = "agenda-buddy.example";
    private const string AnonymousRoute = "api/v1/professions";

    private static readonly Dictionary<string, string> HstsOn = new()
    {
        ["Security:Hsts:Enabled"] = "true",
        ["Security:Hsts:MaxAgeDays"] = "30"
    };

    [Theory]
    [InlineData("https")]
    [InlineData("http")]
    public async Task T303_GatewayForwardedRequest_TransportSecurityBehaviorMatchesDirectCall(string scheme)
    {
        using var backend = host.StartService(settings: HstsOn);
        using var gatewayFactory = GatewayToRealServiceHarness.CreateFactory(
            "profession", backend.Server, destinationScheme: scheme);
        using var gateway = gatewayFactory.CreateClient();

        var direct = await backend.Client.GetAsync($"{scheme}://{ExternalHost}/{AnonymousRoute}");
        // The client-facing hop here is deliberately a DIFFERENT host/scheme pairing than the destination
        // scheme under test (see class remarks) — proving Profession's own behaviour tracks what IT
        // received, not what the mobile app sent the gateway.
        var throughGateway = await gateway.GetAsync($"https://mobile-app.example/{AnonymousRoute}");

        Assert.Equal(direct.StatusCode, throughGateway.StatusCode);

        Assert.Equal(
            direct.Headers.Contains(HstsHeader), throughGateway.Headers.Contains(HstsHeader));

        if (direct.Headers.Contains(HstsHeader))
        {
            Assert.Equal(
                direct.Headers.GetValues(HstsHeader).Single(),
                throughGateway.Headers.GetValues(HstsHeader).Single());
        }

        // No redirect loop, and no redirect at all — matching the direct call. UseHttpsRedirection is a
        // no-op with no HTTPS port configured (TransportSecurity.cs), which is true for both paths here;
        // this asserts that stays true when a proxy hop and its X-Forwarded-* headers are in the mix.
        Assert.False(IsRedirect(direct.StatusCode), $"Direct call unexpectedly redirected: {direct.StatusCode}");
        Assert.False(
            IsRedirect(throughGateway.StatusCode),
            $"Gateway-forwarded call unexpectedly redirected: {throughGateway.StatusCode}");
    }

    [Fact]
    public async Task T303_WithTheDefaultConfiguration_NeitherPathSendsHsts()
    {
        // AC14's direct-call equivalent (TransportSecurityHeaderTest), proved to also hold through the
        // gateway: the absence of Security:Hsts:Enabled means no header on either path.
        using var backend = host.StartService();
        using var gatewayFactory = GatewayToRealServiceHarness.CreateFactory("profession", backend.Server);
        using var gateway = gatewayFactory.CreateClient();

        var direct = await backend.Client.GetAsync($"https://{ExternalHost}/{AnonymousRoute}");
        var throughGateway = await gateway.GetAsync($"https://mobile-app.example/{AnonymousRoute}");

        Assert.DoesNotContain(HstsHeader, direct.Headers.Select(h => h.Key));
        Assert.DoesNotContain(HstsHeader, throughGateway.Headers.Select(h => h.Key));
    }

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        (int)statusCode is >= 300 and < 400;
}
