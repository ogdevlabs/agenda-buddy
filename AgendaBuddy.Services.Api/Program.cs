using AgendaBuddy.Library.Tools;
using Microsoft.Extensions.Caching.Distributed;

ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;


var builder = WebApplication.CreateBuilder(args);

// Aspire defaults: telemetry, health checks, service discovery, HttpClient resilience.
builder.AddServiceDefaults();

// One MongoDB client per process, shared by the repositories and the EventStore. Aspire injects ConnectionStrings:mongodb; the resolver also accepts every legacy
// shape, and fails with a message naming each key it tried rather than a null-argument throw.
builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(MongoConnectionResolver.Resolve(builder.Configuration)));

// Cross-service revocation denylist -- every service that authenticates a bearer token needs to check it.
builder.Services.AddTokenRevocationStore(builder.Configuration);

// Readiness probe. Singleton so the 5s result cache is process-wide.
builder.Services.AddSingleton<MongoHealthCheck>();
builder.Services.AddHealthChecks()
    .AddCheck<MongoHealthCheck>("mongodb", tags: ["ready"]);
// Add MongoDB
builder.Services.AddMongoDbRepository(builder.Configuration);
// Add Cache
builder.Services.AddDistributedMemoryCache();

// Add MediatR
// Handlers live in AgendaBuddy.Services.Core, a separate assembly from AgendaBuddy.Services.Api --
// MediatR's RegisterServicesFromAssembly only scans the one assembly it's given, so both must be
// registered or mediator.Send(command/query) throws "no handler registered" at runtime, not at compile
// time.
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblies(typeof(Program).Assembly, typeof(GetServicesFromProviderQueryHandler).Assembly));
builder.Services.AddEventStore();
// Add services required to support using MVC's model binders
builder.Services.AddMvcCore();

// Enable & configure JSON Problem Details error responses
// ADR-022: ForbiddenException -> 403 centrally, so an endpoint that omits a local
// try/catch returns 403 rather than a bare 500. Registered unconditionally, unlike the
// Development-only UseExceptionHandler lambda below.
builder.Services.AddExceptionHandler<AgendaBuddyExceptionHandler>();
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails =
        context => CustomizeProblemDetails(context.ProblemDetails, context.HttpContext));

// Add Anti-CSRF/XSRF services
builder.Services.AddAntiforgery();

// JWT Bearer authentication (reads JWT_PUBLIC_KEY env var — fails fast if absent)
builder.Services.AddAgendaBuddyAuthentication();
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Idempotent -- safe on every startup across all seven processes sharing this collection.
// Swallowed on failure, same as Profession's seed hosted service: an unreachable Mongo at
// boot (e.g. the OpenApiSpecGenerator harness, which deliberately never touches a real one)
// must not prevent the host from starting -- the denylist check itself already fails open
// per-request if the collection or its index is missing.
try
{
    await app.Services.GetRequiredService<MongoTokenRevocationStore>().EnsureIndexAsync();
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Could not ensure the revoked_tokens TTL index at startup");
}

// /health runs every check; /alive only the live-tagged ones, so a service waiting on MongoDB is
// not restarted for being unready.
app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    // Error handling
    app.UseExceptionHandler(new ExceptionHandlerOptions
    {
        AllowStatusCode404Response = true,
        ExceptionHandler = async exceptionContext =>
        {
            // GitHub issue to support this in framework: https://github.com/dotnet/aspnetcore/issues/43831
            var exceptionHandlerFeature = exceptionContext.Features.Get<IExceptionHandlerFeature>();

            if (exceptionHandlerFeature?.Error is BadHttpRequestException badRequestEx)
                exceptionContext.Response.StatusCode = badRequestEx.StatusCode;

            if (exceptionContext.Request.AcceptsJson()
                && exceptionContext.RequestServices.GetRequiredService<IProblemDetailsService>() is
                { } problemDetailsService)
            {
                // Write as JSON problem details
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

// MUST stay AFTER the IsDevelopment() block. Middleware registered earlier is outermost and an
// exception propagates outward, so the INNERMOST handler sees it first. Placed here, this one takes
// ForbiddenException and declines everything else, which then rethrows and reaches the Development
// lambda exactly as it does today. Placed BEFORE that block, the lambda would swallow
// ForbiddenException and the central 403 would fail in Development only. See AgendaBuddyExceptionHandler.
app.UseExceptionHandler();

// HSTS (under its flag) and the HTTPS redirect must run BEFORE authentication. Registered after
// UseAuthentication, the redirect would parse and validate the bearer token out of a plaintext
// request and only then tell the client to come back over TLS.
app.UseAgendaBuddyTransportSecurity();

app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();
app.UseStatusCodePages();

var services = app.MapGroup("api/v1/services")
    .WithTags("ServiceAPI")
    .WithOpenApi()
    .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

services.MapGet("/{email}",
    async Task<Results<Ok<DataResponse<List<ServiceEntity>>>, NotFound>> (
        IMediator mediator,
        string email,
        IDistributedCache cache,
        CancellationToken cancellationToken) =>
    {
        var key = $"services-{email}";

        // Dispatched through mediator.Send with the request's CancellationToken. A missing provider is
        // a successful empty read (see the handler's own remarks), so this Fail-to-null mapping and the
        // NotFound branch below are unreachable in practice -- preserved anyway, matching every other
        // migrated service's shape.
        var serviceEntities = await cache.GetOrCreateAsync(key, async token =>
        {
            var result = await mediator.Send(new GetServicesFromProviderQuery { Email = email }, token);
            return result.IsSuccess ? result.Value : null!;
        }, cancellationToken: cancellationToken);

        if (serviceEntities is not null)
            return TypedResults.Ok(DataResponse<List<ServiceEntity>>.Ok(serviceEntities));

        return TypedResults.NotFound();
    })
    // PII-bearing read, so no longer anonymous. Breaking change with zero reachable consumers
    // (01-api-surface.md:158).
    .WithName("GetServicesFromProvider")
    .RequireAuthorization();

services.MapPut("/{email}",
    async Task<Results<ValidationProblem, NotFound, Ok<DataResponse<ProviderEntity>>>> (
        IMediator mediator,
        ClaimsPrincipal user,
        [FromBody] List<ServiceEntity> serviceEntities,
        string email,
        CancellationToken cancellationToken) =>
    {
        if (!MiniValidator.TryValidate(serviceEntities, out var errors))
            return TypedResults.ValidationProblem(errors);

        // No local try/catch: AgendaBuddyExceptionHandler maps ForbiddenException to 403 centrally.
        OwnershipGuard.AssertOwner(user, email);

        var result = await mediator.Send(new AddServicesToProviderCommand
        {
            Email = email,
            ServiceEntities = serviceEntities
        }, cancellationToken);

        if (result.IsSuccess)
            return TypedResults.Ok(DataResponse<ProviderEntity>.Ok(result.Value));

        return TypedResults.NotFound();
    })
    .WithName("AddServicesToProvider")
    .RequireAuthorization();

services.MapPatch("/{email}",
    async Task<Results<ValidationProblem, NotFound, Ok<DataResponse<ProviderEntity>>>> (
        IMediator mediator,
        ClaimsPrincipal user,
        [FromBody] List<ServiceEntity> serviceEntities,
        string email,
        CancellationToken cancellationToken) =>
    {
        if (!MiniValidator.TryValidate(serviceEntities, out var errors))
            return TypedResults.ValidationProblem(errors);

        // No local try/catch: AgendaBuddyExceptionHandler maps ForbiddenException to 403 centrally.
        OwnershipGuard.AssertOwner(user, email);

        var result = await mediator.Send(new UpdateServicesFromProviderCommand
        {
            Email = email,
            ServiceEntities = serviceEntities
        }, cancellationToken);

        if (result.IsSuccess)
            return TypedResults.Ok(DataResponse<ProviderEntity>.Ok(result.Value));

        return TypedResults.NotFound();
    })
    .WithName("UpdateServicesFromProvider")
    .RequireAuthorization();

app.Run();

// Functions and Methods
void CustomizeProblemDetails(ProblemDetails problemDetails, HttpContext httpContext)
{
    problemDetails.Extensions["requestId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
}
