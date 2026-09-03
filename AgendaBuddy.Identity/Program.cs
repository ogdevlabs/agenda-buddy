using AgendaBuddy.Identity.Modules;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AgendaBuddy.Identity.Requests;
using AgendaBuddy.Identity.Services;
using AgendaBuddy.Library.Services;
using AgendaBuddy.Library.Tools;

ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

var builder = WebApplication.CreateBuilder(args);

// Aspire defaults: telemetry, health checks, service discovery, HttpClient resilience.
builder.AddServiceDefaults();

// One MongoDB client per process, shared by the repositories. Identity registers no EventStore — it has
// no CQRS command handlers to audit. Aspire injects ConnectionStrings:mongodb; the resolver also accepts
// every legacy shape, and fails with a message naming each key it tried rather than a null-argument throw.
builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(MongoConnectionResolver.Resolve(builder.Configuration)));

// Cross-service revocation denylist -- every service that authenticates a bearer token needs to check it.
builder.Services.AddTokenRevocationStore(builder.Configuration);

// Readiness probe. Singleton so the 5s result cache is process-wide.
builder.Services.AddSingleton<MongoHealthCheck>();
builder.Services.AddHealthChecks()
    .AddCheck<MongoHealthCheck>("mongodb", tags: ["ready"]);

// Add MongoDB — IdentityDb / credentials collection
builder.Services.AddMongoDbRepository(builder.Configuration);

// Add MediatR
builder.Services.AddMediatR(cfg => { cfg.RegisterServicesFromAssembly(typeof(Program).Assembly); });

// Add services required to support using MVC's model binders
builder.Services.AddMvcCore();

// Register instances
builder.Services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
builder.Services.AddScoped<IdentityService>();
builder.Services.AddScoped<IDeviceTokenService, DeviceTokenService>();

// Email delivery for the two messages that carry a token the recipient cannot obtain any other way:
// email confirmation and password reset. With no Email:ApiKey configured this is a logged no-op, so a
// local run needs no mail provider — but a deployed environment without it has no working password
// reset, which is why the startup warning below names the key.
builder.Services.AddEmailDelivery(builder.Configuration);

// Lockout thresholds. No enable flag — with the defaults an account locks only after 10
// consecutive wrong passwords and unlocks itself 15 minutes later, so there is nothing a local run
// needs switched off, and a third flag would only be a third way for a control to go missing.
builder.Services.Configure<LockoutOptions>(
    builder.Configuration.GetSection(LockoutOptions.Section));

// Per-IP limiting on the two routes that spend BCrypt. Read eagerly because the
// flag decides whether the limiter is registered at all — with it off, neither this nor UseRateLimiter
// runs and the pipeline is exactly what it was before this limiter existed, which is what makes the
// feature revertible by configuration alone.
var rateLimiting = new RateLimitingOptions();
builder.Configuration.GetSection(RateLimitingOptions.Section).Bind(rateLimiting);
if (rateLimiting.Enabled) builder.Services.AddAuthRateLimiter(rateLimiting);

// JWT Bearer authentication (reads JWT_PUBLIC_KEY env var — fails fast if absent)
builder.Services.AddAgendaBuddyAuthentication();
builder.Services.AddAuthorization();

// Enable & configure JSON Problem Details error responses
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context => CustomizeProblemDetails(context.ProblemDetails, context.HttpContext));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCarter(configurator: c => c.WithModule<AuthModule>().WithModule<DeviceTokenModule>());

var app = builder.Build();

// Idempotent -- safe on every startup across all seven processes sharing this collection.
// Fire-and-forget, not awaited: MongoDB's server-selection timeout is ~30s by default, and
// awaiting this inline would stall Kestrel's own startup for that long whenever Mongo isn't
// immediately reachable (found live -- it pushed every service's CI boot check right up to,
// and for one, past, its readiness window). Swallowed on failure for the same reason
// Profession's seed hosted service swallows its own: the denylist check itself already
// fails open per-request if the collection or its index is missing.
_ = Task.Run(async () =>
{
    try
    {
        await app.Services.GetRequiredService<MongoTokenRevocationStore>().EnsureIndexAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not ensure the revoked_tokens TTL index at startup");
    }
});

// /health runs every check; /alive only the live-tagged ones, so a service waiting on MongoDB is
// not restarted for being unready.
app.MapDefaultEndpoints();

// FIRST in the pipeline, and that is the whole point: a throttled request must be
// refused before it can reach BCrypt or the database, so it costs no CPU and takes no write. A limiter
// registered behind the handler would still let the denial of service land. Routing runs ahead of any
// middleware registered here — WebApplication inserts it at the head of the pipeline when it is not
// called explicitly — so the per-endpoint policy metadata is already resolved by this point.
if (rateLimiting.Enabled) app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseExceptionHandler(new ExceptionHandlerOptions
    {
        AllowStatusCode404Response = true,
        ExceptionHandler = async exceptionContext =>
        {
            var exceptionHandlerFeature = exceptionContext.Features.Get<IExceptionHandlerFeature>();

            if (exceptionHandlerFeature?.Error is BadHttpRequestException badRequestEx)
                exceptionContext.Response.StatusCode = badRequestEx.StatusCode;

            if (exceptionContext.Request.AcceptsJson()
                && exceptionContext.RequestServices.GetRequiredService<IProblemDetailsService>() is
                { } problemDetailsService)
            {
                await problemDetailsService.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = exceptionContext,
                    AdditionalMetadata = exceptionHandlerFeature?.Endpoint?.Metadata,
                    ProblemDetails = { Status = exceptionContext.Response.StatusCode }
                });
            }
            else
            {
                exceptionContext.Response.ContentType = "text/plain";
                var message = ReasonPhrases.GetReasonPhrase(exceptionContext.Response.StatusCode) switch
                {
                    { Length: > 0 } reasonPhrase => reasonPhrase,
                    _ => "An error occurred"
                };
                await exceptionContext.Response.WriteAsync(message + "\r\n");
                await exceptionContext.Response.WriteAsync(
                    $"Request ID: {Activity.Current?.Id ?? exceptionContext.TraceIdentifier}");
            }
        }
    });
}

// SECURITY: UseHttpLogging is intentionally NOT registered.
// Request/response body logging is absent to prevent plaintext passwords and
// JWT bearer tokens (which carry the email as the 'sub' claim — PII per CONSTITUTION §4)
// from appearing in log output. Do not add UseHttpLogging or any request body
// logging middleware without first excluding POST /api/v1/auth/login and
// POST /api/v1/auth/device-token from the logged paths.
// Identity is API-only — no HTML forms, no antiforgery
// HSTS (under its flag) and the HTTPS redirect run BEFORE authentication.
// This service receives plaintext passwords, and its redirect previously ran last — and only outside
// Development, a condition that meant nothing here because the AppHost runs every service as
// Production (ARCHITECTURE.md D-6). The environment guard is gone: the flag is the switch now, and the
// redirect is a no-op wherever no HTTPS port is configured.
// `includeRateLimitingInAudit` is true for Identity alone: it owns the only two routes that spend
// BCrypt, so it is the only service where a missing limiter flag is worth a startup warning.
app.UseAgendaBuddyTransportSecurity(includeRateLimitingInAudit: true, includeEmailDeliveryInAudit: true);

app.UseAuthentication();
app.UseAuthorization();
app.UseStatusCodePages();

app.MapCarter();

app.Run();

void CustomizeProblemDetails(ProblemDetails problemDetails, HttpContext httpContext)
{
    problemDetails.Extensions["requestId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
}
