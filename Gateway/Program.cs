using Gateway;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Aspire defaults: telemetry, health checks, service discovery, HttpClient resilience — the same call as
// all seven domain services (see Booking/Program.cs). Inherits PiiRedactingProcessor automatically.
builder.AddServiceDefaults();

// F-015-T02 spike: routes/clusters are built programmatically from the same Aspire service-discovery
// configuration keys (services__<name>__http__0) that AddServiceDefaults() above already resolves for
// every service's own outbound HttpClient calls — never a static appsettings.json cluster file
// (ARCHITECTURE.md §2/§5). AspireServiceDiscoveryProxyConfigProvider re-polls IConfiguration on an
// interval so a destination's reassigned port is (attempted to be) picked up without this process
// restarting; see ARCHITECTURE.md §6 for what the spike actually found when tested against a live
// AppHost restart. One destination ("booking") is enough to prove the mechanism — F-015-T03 covers the
// full seven-service allowlist.
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

// F-015-T02 spike: proxies the one route the spike's Gateway->booking wiring configures. F-015-T03
// replaces AspireServiceDiscoveryProxyConfigProvider's single-destination table with the full
// seven-service allowlist and failure-translation middleware from ARCHITECTURE.md §2.
app.MapReverseProxy();

app.Run();
