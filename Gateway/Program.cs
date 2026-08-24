using System.Diagnostics;
using Gateway;
using Yarp.ReverseProxy.Configuration;

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
builder.Services.AddReverseProxy();

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
