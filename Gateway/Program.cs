var builder = WebApplication.CreateBuilder(args);

// Aspire defaults: telemetry, health checks, service discovery, HttpClient resilience — the same call as
// all seven domain services (see Booking/Program.cs). Inherits PiiRedactingProcessor automatically.
builder.AddServiceDefaults();

var app = builder.Build();

// F-021 PRD requirement 13 / the project-wide middleware-order convention: HSTS (under its flag) and the
// HTTPS redirect must run BEFORE authentication, or a bearer token is parsed out of a plaintext request
// before the client is told to come back over TLS. Gateway has no authentication middleware of its own —
// ARCHITECTURE.md §2's "Auth passthrough" decision means it forwards the Authorization header byte-for-
// byte once F-015-T03 adds YARP — but the call and its position are still required so
// Library.Tests/Security/TransportSecurityOrderTest.cs holds for the eighth service exactly as it does
// for the other seven.
app.UseAgendaBuddyTransportSecurity();

// /health (readiness, every check) and /alive (liveness, live-tagged only) — identical wiring to the
// seven services. No YARP, no routing yet: F-015-T03 adds app.MapReverseProxy() and the route/cluster
// config built from Aspire service discovery. This task only proves the eighth process starts, reports
// healthy, and exports telemetry the same way the other seven do.
app.MapDefaultEndpoints();

app.Run();
