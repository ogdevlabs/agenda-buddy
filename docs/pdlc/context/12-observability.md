# 12 — Observability

**Summary: one of the four pillars exists, and only in its default form.**

| Pillar | Status |
|---|---|
| **Logging** | ⚠️ Configuration only — default console provider, no structured sink, almost no application logging |
| **Metrics** | ❌ **Absent** — no `Meter`, no counters, no Prometheus, no `/metrics` |
| **Tracing** | ❌ **Absent** — no OpenTelemetry, no exporter, no propagation |
| **Health** | ❌ **Absent** — no `AddHealthChecks`, no `MapHealthChecks`, no endpoint |

Verified by grep across all `*.cs` and `*.csproj` (excluding `bin`/`obj`): zero matches for `OpenTelemetry`, `ActivitySource`, `Meter(`, `AddHealthChecks`, `MapHealthChecks`, `ServiceDefaults`, `Aspire`, `Polly`, `ResilienceHandler`, `AddStandardResilienceHandler`.

This is the single largest operational gap in the codebase and it compounds every other finding: a service that cannot be probed, traced, or measured cannot be safely deployed or diagnosed.

---

## Logging — what exists

### Configuration

Only `Logging.LogLevel` sections in `appsettings*.json`. No provider is added or removed in code — every service uses the ASP.NET default (`Console` + `Debug` + `EventSource`).

| Service | `Default` | Other overrides | Anchor |
|---|---|---|---|
| Booking | Information | `Provider: Debug`, `Microsoft.AspNetCore: Warning` | `Booking/appsettings.json:2-8` |
| Calendar | Information | `Provider: Debug`, `Microsoft.AspNetCore: Warning` | `Calendar/appsettings.json:2-8` |
| Provider | Information | `Provider: Debug`, `Microsoft.AspNetCore: Warning` | `Provider/appsettings.json:2-8` |
| Customer | Information | `Microsoft.AspNetCore: Warning` | `Customer/appsettings.json:2-7` |
| Services | Information | `Microsoft.AspNetCore: Warning` | `Services/appsettings.json:2-7` |
| Profession | Information | `Microsoft.AspNetCore: Warning` | `Profession/appsettings.json:2-7` |
| Identity | Information | `Microsoft.AspNetCore: Warning`; Development adds `Identity: Debug` | `Identity/appsettings.json:2-7`, `Identity/appsettings.Development.json:2-8` |
| EventAndCommands | **Debug** | `System: Information`, `Microsoft: Information` | `EventAndCommands/appsettings.json:2-8` |

⚠️ **`Provider: Debug` in Booking and Calendar is meaningless** — `"Provider"` is not a namespace in either assembly. Copy-paste residue (`06-configuration.md`).

⚠️ **`EventAndCommands` ships a `Debug` default level** in a class library, and the file is copied to output on every build (`EventAndCommands.csproj:26-28`). It is only read by the dead `ConfigurationLoader`, so it has no effect — but if that path were ever revived it would flip a library to verbose logging.

⚠️ **`MobileApp` adds only `AddDebug()`, and only in Debug builds** (`MobileApp/MauiProgram.cs:21-23`). A Release build of the mobile app has **no logging provider at all**.

### Application logging in code

`ILogger` is injected in exactly **three** places, and two of them are dead code:

| Site | Anchor | Status |
|---|---|---|
| `SeedAuthCredentials.RunAsync` | `Library/Tools/Migrations/SeedAuthCredentials.cs:45,77` | ⚠️ **never invoked** (`05-data-model.md`) |
| `SeedDevelopmentAccounts.RunAsync` | `Library/Tools/Migrations/SeedDevelopmentAccounts.cs:33,41,53,61,65,96` | ⚠️ **never invoked** |
| `AuthenticationExtensions.LogKeyFingerprint` | `Library.ServerAuth/AuthenticationExtensions.cs:52-63` | ✅ live — logs the RSA public-key SHA-256 fingerprint at startup |

⚠️ **No `Library/Services/*` domain service logs anything.** Not one `ILogger` injection across the 13 services. A booking that silently fails (`BookingAppointmentCommandHandler.cs:41` returns `null!`) leaves no log line — only a `"Failed"` document in the Mongo `events` collection.

⚠️ **No `Program.cs` logs anything.** No startup banner, no configuration summary, no shutdown log.

⚠️ **The exception handler does not log.** `Booking/Program.cs:43-79` writes a response and returns without touching `ILogger`. ASP.NET's `ExceptionHandlerMiddleware` logs at `Error` by default, but with no context enrichment — and the handler is **Development-only** (`10-error-handling.md`), so in production the built-in logging path is not even reached.

⚠️ **`AuthenticationExtensions.LogKeyFingerprint:54` calls `services.BuildServiceProvider()`** to obtain an `ILoggerFactory` during DI registration. This is the ASP0000 anti-pattern: it constructs a **second, throwaway container**, duplicating every singleton registered so far and creating a second set of disposables that are never disposed. It is done solely to emit one informational log line. See `13-security.md`.

### Log hygiene — the one deliberate control

`Identity/Program.cs:81-86`:

```
// SECURITY (T-001): UseHttpLogging is intentionally NOT registered.
// Request/response body logging is absent to prevent plaintext passwords and
// JWT bearer tokens (which carry the email as the 'sub' claim — PII per CONSTITUTION §4)
// from appearing in log output. Do not add UseHttpLogging or any request body
// logging middleware without first excluding POST /api/v1/auth/login and
// POST /api/v1/auth/device-token from the logged paths.
```

This is enforced by test: `Identity.Tests/Security/LoginLogSanitizationTest.cs` (4 tests). A rare case of a documented control with a regression guard.

⚠️ **The comment names `POST /api/v1/auth/device-token`, but the actual route is `POST /device-token`** — it is mapped on `app`, not the `auth` group (`Identity/Program.cs:154`). Anyone following the comment's instruction would exclude a path that does not exist (`01-api-surface.md`).

⚠️ **`UseHttpLogging` is absent from the other six services too, but by omission rather than decision** — no comment, no test. Booking, Customer, Provider, Services all accept bodies containing customer and provider emails; a future `UseHttpLogging` there would leak PII with nothing to stop it.

### What logging cannot do here

- ⚠️ **No structured logging sink.** No Serilog, NLog, Seq, Elastic, Application Insights, Datadog, or OTLP log exporter. Output is unstructured console text, lost when the container stops.
- ⚠️ **No log aggregation.** Seven independent processes writing to seven consoles with no shipper. Correlating a single user action across services is impossible — and the `requestId` returned to clients (`10-error-handling.md`) cannot be looked up anywhere.
- ⚠️ **No scope enrichment.** No `BeginScope`, no user/tenant/correlation id on any log line.
- ⚠️ **No log retention or rotation policy.**

---

## Metrics — absent

No `System.Diagnostics.Metrics.Meter`, no counter, no histogram, no gauge. No `prometheus-net`, no `App.Metrics`, no `/metrics` endpoint.

The framework's built-in `Microsoft.AspNetCore.Hosting` and `System.Net.Http` meters emit by default in .NET 8+, but **nothing collects or exports them** — no `AddOpenTelemetry().WithMetrics()`, no `IMetricsListener`. They are produced and discarded.

Consequences given the other findings:
- The `CacheAside` timeout path that returns `null` and produces spurious 404/204 responses (`04-data-access.md`) would be invisible — no cache-hit-rate, no lock-timeout counter.
- The Kafka `CreateTopicIfNotExist` 10-second timeout blocking user registration (`09-integrations.md`) would show only as slow requests with no attribution.
- Mongo full-collection scans (`DeviceTokenService.GetByEmailAsync`, `CalendarService.GetAllAppointmentsAsync`) have no query-duration signal.

⚠️ `docker-compose.override.yml:33-46` configures **Kafka broker** metrics (`KAFKA_JMX_PORT: 9101`, `CONFLUENT_METRICS_ENABLE: 'true'`, `CONFLUENT_METRICS_REPORTER_*`) and `kafka-ui` reads `KAFKA_CLUSTERS_0_METRICS_PORT: 9101` (`:6`). So **the only component with metrics wired is the message broker the application never publishes to.**

---

## Tracing — absent

No `ActivitySource` is created anywhere. No OpenTelemetry package, no exporter, no `AddSource`, no W3C context propagation configuration.

`Activity.Current?.Id` **is** read — in all seven `CustomizeProblemDetails` local functions and in the plain-text error branch (`Booking/Program.cs:76,173`). ASP.NET Core creates an `Activity` per request automatically, so this yields a real trace-id. But:

⚠️ **The trace-id is emitted to the client and stored nowhere.** No trace backend, no log correlation, no sampling. `requestId` is a token the client can quote and support cannot resolve.

⚠️ **There is nothing to trace across.** The services never call each other (`09-integrations.md`), so distributed tracing would show seven disconnected single-span traces. The trace gap is a symptom of the shared-database integration pattern, not just of missing instrumentation.

⚠️ **The MongoDB driver 2.25.0 has no built-in OTel instrumentation**, and no `MongoDB.Driver.Core.Extensions.DiagnosticSources` package is referenced — so even with OTel added, database spans would need explicit wiring.

---

## Health checks — absent

No `builder.Services.AddHealthChecks()` and no `app.MapHealthChecks(...)` in any of the seven services. No `/health`, `/healthz`, `/ready`, or `/live` route.

This has concrete consequences already visible in the repo:

- ⚠️ **No `HEALTHCHECK` in any of the 8 Dockerfiles** — there is no endpoint to point one at (`08-cicd-deploy.md`).
- ⚠️ **`docker-compose.override.yml:126-127` uses a bare `depends_on: [mongo]` for `identity`** because there is no Mongo healthcheck and no Identity healthcheck to gate on. Identity can start before Mongo accepts connections; `IdentityService.IsMongoDown` (`:228`) converts the resulting failures into HTTP 503, so it degrades rather than crashes — but the first requests after `docker compose up` fail with no readiness signal to prevent traffic.
- ⚠️ **The `broker` service is the only container with a healthcheck** (`:48-52`, `kafka-topics --list`), and `schema-registry` is the only consumer of it (`docker-compose.yml:29-30`). Again: the infrastructure the app does not use is the best-instrumented part of the system.
- ⚠️ **No dependency probe.** Nothing verifies Mongo reachability, JWT key presence, or Kafka availability at startup — except the two hard fail-fast checks: `AuthenticationExtensions.cs:18-21` throws if `JWT_PUBLIC_KEY` is missing (all services), and `MongoDbConfiguration.cs:7` throws on a null connection string (which is exactly what happens outside Development, `06-configuration.md`). These are crashes, not health signals.

⚠️ **`JWT_PRIVATE_KEY` is checked lazily** (`IdentityService.cs:189-191`), so Identity **starts successfully** without it and fails on the first login attempt. A readiness probe would have caught this at deploy time; there is none.

---

## Dashboards and alerts

`[unknown — outside repo]` — no dashboard definitions, alert rules, SLO documents, or runbooks are committed. `docs/pdlc/memory/DEPLOYMENTS.md` exists in the PDLC memory bank but no monitoring configuration accompanies it.

⚠️ **No error-rate, latency, or saturation alerting is possible** with the current instrumentation. There is no signal to alert on.

---

## Improvement opportunities, ranked by leverage

1. **Health endpoints** — `AddHealthChecks()` with a Mongo probe and a JWT-key-presence check, exposed at `/health` (liveness) and `/ready` (readiness), plus `HEALTHCHECK` in each Dockerfile and `condition: service_healthy` in Compose. Unblocks safe container orchestration and fixes the `depends_on` race.
2. **OpenTelemetry, traces + metrics + logs** — one shared registration (ASP.NET Core, `HttpClient`, and MongoDB instrumentation) with an OTLP exporter. Makes the existing `Activity.Current?.Id`/`requestId` mechanism actually resolvable, and gives the `CacheAside` and Kafka-timeout pathologies a visible signal.
3. **A structured log sink with correlation** — replace the default console provider, add scope enrichment (user email hash, route, `requestId`), and ship to one destination for all seven processes.
4. **Domain-level logging in `Library/Services`** — at minimum, log every path that currently returns `null!` or a `"exception…"` sentinel string (`10-error-handling.md`), since those are silent failures today.
5. **Coverage/quality signals in CI** — the coverage artifact is uploaded and never read (`11-testing.md`); a threshold gate is a cheap first metric.
6. **Resilience instrumentation** — adding `AddStandardResilienceHandler` would bring both retry policies *and* the resilience meter, currently both absent.

Note that items 1 and 2 are exactly what a **.NET Aspire `ServiceDefaults`** project provides out of the box (health endpoints, OpenTelemetry wiring, and `HttpClient` resilience + service discovery), which is directly relevant to the F-013 `aspire-wiring` work this catalog was hydrated for.
