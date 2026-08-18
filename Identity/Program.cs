using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Identity.Requests;
using Identity.Services;
using Library.Services;
using Library.Tools;

ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

var builder = WebApplication.CreateBuilder(args);

// Aspire defaults: telemetry, health checks, service discovery, HttpClient resilience.
builder.AddServiceDefaults();

// One MongoDB client per process, shared by the repositories and the EventStore. Aspire injects ConnectionStrings:mongodb; the resolver also accepts every legacy
// shape, and fails with a message naming each key it tried rather than a null-argument throw.
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

// SECURITY (T-001): UseHttpLogging is intentionally NOT registered.
// Request/response body logging is absent to prevent plaintext passwords and
// JWT bearer tokens (which carry the email as the 'sub' claim — PII per CONSTITUTION §4)
// from appearing in log output. Do not add UseHttpLogging or any request body
// logging middleware without first excluding POST /api/v1/auth/login and
// POST /api/v1/auth/device-token from the logged paths.
// Identity is API-only — no HTML forms, no antiforgery
app.UseAuthentication();
app.UseAuthorization();
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

var auth = app.MapGroup("api/v1/auth")
    .WithTags("IdentityAPI")
    .WithOpenApi();

auth.MapPost("/register", async (RegisterRequest req, IdentityService svc) =>
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

auth.MapPost("/login", async (LoginRequest req, IdentityService svc) =>
{
    try
    {
        var result = await svc.LoginAsync(req.Email, req.Password);
        return Results.Ok(new { accessToken = result!.AccessToken, refreshToken = result.RefreshToken });
    }
    catch (UnauthorizedException) { return Results.Unauthorized(); }
    catch (ServiceUnavailableException ex) { return Results.Problem(detail: ex.Message, statusCode: 503, title: "service_unavailable"); }
}).WithName("Login");

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
