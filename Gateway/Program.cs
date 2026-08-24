using System.Diagnostics;
using Gateway;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// Aspire defaults: telemetry, health checks, service discovery, HttpClient resilience — the same call as
// all seven domain services (see Booking/Program.cs). Inherits PiiRedactingProcessor automatically.
builder.AddServiceDefaults();

// F-015-T02 spiked this against a single ("booking") destination; F-015-T03 expands
// AspireServiceDiscoveryProxyConfigProvider to the full seven-service api/v1/{service}/** allowlist
// (plus api/v1/auth/** and the root-mapped /device-token, both -> identity) — routes/clusters are built
// programmatically from the same Aspire service-discovery configuration keys
// (services__<name>__http__0) that AddServiceDefaults() above already resolves for every service's own
// outbound HttpClient calls — never a static appsettings.json cluster file (ARCHITECTURE.md §2/§5).
// AspireServiceDiscoveryProxyConfigProvider re-polls IConfiguration on an interval so a destination's
// reassigned port is (attempted to be) picked up without this process restarting; see ARCHITECTURE.md §6
// for what the spike actually found when tested against a live AppHost restart.
builder.Services.AddSingleton<IProxyConfigProvider, AspireServiceDiscoveryProxyConfigProvider>();

// F-015-T04 / PRD AC5: a response transform, registered once via AddTransforms so it runs for every
// route regardless of which of the seven clusters it targets, rewrites a destination failure into the
// gateway-destination-unreachable ProblemDetails shape (api-contracts.md §1) instead of letting YARP's
// bare 502/504 (or the destination's own untranslated 5xx body) reach MobileApp.
builder.Services.AddReverseProxy()
    .AddTransforms(transformBuilderContext =>
        transformBuilderContext.AddResponseTransform(TranslateDestinationFailureAsync));

var app = builder.Build();

// F-021 PRD requirement 13 / the project-wide middleware-order convention: HSTS (under its flag) and the
// HTTPS redirect must run BEFORE authentication, or a bearer token is parsed out of a plaintext request
// before the client is told to come back over TLS. Gateway has no authentication middleware of its own —
// ARCHITECTURE.md §2's "Auth passthrough" decision means it forwards the Authorization header byte-for-
// byte — but the call and its position are still required so
// Library.Tests/Security/TransportSecurityOrderTest.cs holds for the eighth service exactly as it does
// for the other seven.
app.UseAgendaBuddyTransportSecurity();

// /health (readiness, every check) and /alive (liveness, live-tagged only) — identical wiring to the
// seven services.
app.MapDefaultEndpoints();

// Proxies every path matched by AspireServiceDiscoveryProxyConfigProvider's explicit
// api/v1/{service}/** allowlist. Anything YARP doesn't match falls through to the MapFallback below —
// the failure-translation middleware for a matched-but-unreachable destination (5xx/timeout) is
// F-015-T04's job, not this one.
app.MapReverseProxy();

// T-302 (threat-model.md): the allowlist above is explicit, never a catch-all — so a path outside every
// configured prefix (e.g. a probe at a backend's own bare /health, reached through the gateway rather
// than an api/v1/{service}/** prefix) matches no YARP route and no other endpoint here. Without this,
// ASP.NET Core's routing default for "no endpoint matched" is a bare 404 with an empty body — not the
// gateway-no-route ProblemDetails shape api-contracts.md §1 specifies. MapFallback is deliberately the
// lowest-priority endpoint in this app: every real route (YARP's, /health, /alive) is tried first, and
// only a genuinely unmatched request reaches this handler.
app.MapFallback(HandleNoRoute);

app.Run();

/// <summary>
/// T-302's mitigation made concrete: the shaped 404 for "no configured route matches this path", never
/// a proxied response. Narrowly scoped to the no-match case only — a matched route whose destination is
/// unreachable or returns 5xx is F-015-T04's failure-translation handler, not this one.
/// </summary>
static IResult HandleNoRoute(HttpContext context) =>
    Results.Problem(
        type: "https://agendabuddy.dev/errors/gateway-no-route",
        title: "No backend service matches this path",
        statusCode: StatusCodes.Status404NotFound,
        detail: $"No destination configured for '{context.Request.Path}'.",
        extensions: new Dictionary<string, object?> { ["requestId"] = Activity.Current?.Id });

/// <summary>
/// F-015-T04 / PRD AC5. YARP's default behavior when a matched destination is unreachable, times out,
/// or itself answers with a 5xx is to proxy through whatever it got (a bare 502/504 with no body on a
/// forwarding failure, or the destination's own untranslated 5xx body) — MobileApp would have to infer
/// which of the seven backend services failed from the route it called. This response transform rewrites
/// both cases into the one shaped ProblemDetails body <c>api-contracts.md</c> §1 specifies, naming the
/// failed cluster so the client's error-display logic doesn't have to guess.
/// </summary>
/// <remarks>
/// <para>
/// <c>ProxyResponse is null</c> is YARP's signal for a forwarding-level failure — connection refused,
/// timed out, or any other exception before a response was received
/// (<c>Yarp.ReverseProxy.Forwarder.IForwarderErrorFeature</c> carries the detail, not read here because
/// the client only needs "which service", not "how it failed"). A destination that DID respond, just
/// with its own 5xx, is the other half of AC5's "5xx" wording; both are folded into the same shape here
/// rather than distinguished, because MobileApp's error-display logic treats them identically.
/// </para>
/// <para>
/// Response transforms run before any bytes reach the client — copying the default response
/// headers/status happens first (see the class remarks on <c>ResponseTransformContext</c>), then each
/// transform gets a chance to override, and only afterward does YARP decide whether to copy a body.
/// Setting <see cref="ResponseTransformContext.SuppressResponseBody"/> is what stops it copying the
/// destination's own body afterward — there is none to copy on the null-response path, but there IS one
/// on the real-5xx path, and letting that copy proceed after this transform has already written its own
/// body would corrupt the response.
/// </para>
/// </remarks>
static async ValueTask TranslateDestinationFailureAsync(ResponseTransformContext responseContext)
{
    var proxyResponse = responseContext.ProxyResponse;
    var isDestinationFailure = proxyResponse is null || (int)proxyResponse.StatusCode >= 500;
    if (!isDestinationFailure)
    {
        return;
    }

    var httpContext = responseContext.HttpContext;
    var failedService = httpContext.GetReverseProxyFeature().Cluster?.Config.ClusterId ?? "unknown";

    // Stops YARP's default body copy running after this transform, and clears whatever headers the
    // default header-copy step (which runs before any transform) already applied from the destination's
    // real 5xx response, so this ProblemDetails body — not the destination's — is what the client sees.
    responseContext.SuppressResponseBody = true;
    httpContext.Response.Headers.Clear();

    await Results.Problem(
        type: "https://agendabuddy.dev/errors/gateway-destination-unreachable",
        title: "The service handling this request is unavailable",
        statusCode: StatusCodes.Status502BadGateway,
        detail: $"The '{failedService}' service did not respond.",
        extensions: new Dictionary<string, object?>
        {
            ["failedService"] = failedService,
            ["requestId"] = Activity.Current?.Id,
        }).ExecuteAsync(httpContext);
}
