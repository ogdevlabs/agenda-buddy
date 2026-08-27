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

var app = builder.Build();

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
app.UseAgendaBuddyTransportSecurity(includeRateLimitingInAudit: true);

app.UseAuthentication();
app.UseAuthorization();
app.UseStatusCodePages();

var auth = app.MapGroup("api/v1/auth")
    .WithTags("IdentityAPI")
    .WithOpenApi();

// Applied to `register` and `login` only, and only when the limiter is registered — RequireRateLimiting
// with no registered policy throws at request time, so the two conditions have to agree. `refresh` and
// `logout` stay unlimited: neither spends BCrypt, and throttling refresh would break the hourly
// rotation a legitimate mobile client performs (D-4).
var register = auth.MapPost("/register", async (RegisterRequest req, IdentityService svc) =>
{
    var emailValidator = new System.ComponentModel.DataAnnotations.EmailAddressAttribute();
    if (string.IsNullOrWhiteSpace(req.Email) || !emailValidator.IsValid(req.Email))
        return Results.BadRequest(new { error = "validation_error", message = "Invalid email format." });
    if (req.Password is null || string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
        return Results.BadRequest(new { error = "validation_error", message = "Password must be at least 8 characters." });
    if (req.Role is not "Provider" and not "Customer")
        return Results.BadRequest(new { error = "validation_error", message = "Role must be 'Provider' or 'Customer'." });

    try
    {
        var result = await svc.RegisterAsync(req.Email, req.Password, req.Role);
        return Results.Created("/api/v1/auth/register", new { accessToken = result!.AccessToken, refreshToken = result.RefreshToken });
    }
    catch (AuthValidationException ex) { return Results.BadRequest(new { error = "validation_error", message = ex.Message }); }
    catch (ConflictException ex) { return Results.Conflict(new { error = "conflict", message = ex.Message }); }
    catch (ServiceUnavailableException ex) { return Results.Problem(detail: ex.Message, statusCode: 503, title: "service_unavailable"); }
}).WithName("Register");

var login = auth.MapPost("/login", async (LoginRequest req, IdentityService svc) =>
{
    try
    {
        var result = await svc.LoginAsync(req.Email, req.Password);
        return Results.Ok(new { accessToken = result!.AccessToken, refreshToken = result.RefreshToken });
    }
    catch (UnauthorizedException) { return Results.Unauthorized(); }
    catch (PasswordResetRequiredException ex) { return Results.Problem(detail: ex.Message, statusCode: 403, title: "password_reset_required"); }
    catch (ServiceUnavailableException ex) { return Results.Problem(detail: ex.Message, statusCode: 503, title: "service_unavailable"); }
}).WithName("Login");

if (rateLimiting.Enabled)
{
    register.RequireRateLimiting(RateLimitingOptions.PolicyName);
    login.RequireRateLimiting(RateLimitingOptions.PolicyName);
}

auth.MapPost("/refresh", async (RefreshRequest req, IdentityService svc) =>
{
    if (string.IsNullOrWhiteSpace(req.RefreshToken))
        return Results.Unauthorized();
    try
    {
        var result = await svc.RefreshAsync(req.RefreshToken);
        return Results.Ok(new { accessToken = result!.AccessToken, refreshToken = result.RefreshToken });
    }
    catch (UnauthorizedException) { return Results.Unauthorized(); }
    catch (ServiceUnavailableException ex) { return Results.Problem(detail: ex.Message, statusCode: 503, title: "service_unavailable"); }
}).WithName("RefreshToken");

auth.MapPost("/logout", async (LogoutRequest req, IdentityService svc) =>
{
    if (string.IsNullOrWhiteSpace(req.RefreshToken))
        return Results.NoContent();
    try
    {
        await svc.LogoutAsync(req.RefreshToken);
        return Results.NoContent();
    }
    catch (ServiceUnavailableException ex) { return Results.Problem(detail: ex.Message, statusCode: 503, title: "service_unavailable"); }
}).WithName("Logout");

// Always 202, whether or not the address matched an account — anti-enumeration, same principle as
// /login's constant-time dummy hash. Unlimited, same reasoning as /refresh: it spends no BCrypt.
auth.MapPost("/password-reset/request", async (PasswordResetRequestRequest req, IdentityService svc) =>
{
    try
    {
        await svc.RequestPasswordResetAsync(req.Email);
    }
    catch (ServiceUnavailableException ex) { return Results.Problem(detail: ex.Message, statusCode: 503, title: "service_unavailable"); }
    return Results.Accepted();
}).WithName("RequestPasswordReset");

auth.MapPost("/password-reset/confirm", async (PasswordResetConfirmRequest req, IdentityService svc) =>
{
    try
    {
        await svc.ConfirmPasswordResetAsync(req.Email, req.Token, req.NewPassword);
        return Results.NoContent();
    }
    catch (AuthValidationException ex) { return Results.BadRequest(new { error = "validation_error", message = ex.Message }); }
    catch (UnauthorizedException ex) { return Results.Problem(detail: ex.Message, statusCode: 401, title: "unauthorized"); }
    catch (ServiceUnavailableException ex) { return Results.Problem(detail: ex.Message, statusCode: 503, title: "service_unavailable"); }
}).WithName("ConfirmPasswordReset");

app.MapPost("/device-token", async (RegisterDeviceTokenRequest request, ClaimsPrincipal user, IDeviceTokenService svc) =>
{
    if (request is null || string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Platform))
        return Results.BadRequest(new { error = "validation_error", message = "Token and platform are required." });

    if (request.Platform is not "android" and not "ios")
        return Results.BadRequest(new { error = "validation_error", message = "Platform must be 'android' or 'ios'." });

    var email = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

    if (string.IsNullOrWhiteSpace(email))
        return Results.Unauthorized();

    await svc.UpsertAsync(email, request.Token, request.Platform);
    return Results.Ok();
}).RequireAuthorization().WithName("RegisterDeviceToken");

app.Run();

void CustomizeProblemDetails(ProblemDetails problemDetails, HttpContext httpContext)
{
    problemDetails.Extensions["requestId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
}
