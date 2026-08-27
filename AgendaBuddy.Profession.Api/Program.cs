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
// Handlers live in AgendaBuddy.Profession.Core, a separate assembly from AgendaBuddy.Profession.Api --
// MediatR's RegisterServicesFromAssembly only scans the one assembly it's given, so both must be
// registered or mediator.Send(query) throws "no handler registered" at runtime, not at compile time.
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblies(typeof(Program).Assembly, typeof(GetProfessionsQueryHandler).Assembly));
builder.Services.AddEventStore();

// Add services required to support using MVC's model binders
builder.Services.AddMvcCore();

// Enable & configure JSON Problem Details error responses
// ADR-022: ForbiddenException -> 403 centrally, so an endpoint that omits a local
// try/catch returns 403 rather than a bare 500. Registered unconditionally, unlike the
// Development-only UseExceptionHandler lambda below.
builder.Services.AddExceptionHandler<AgendaBuddyExceptionHandler>();
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context => CustomizeProblemDetails(context.ProblemDetails, context.HttpContext));

// Add Anti-CSRF/XSRF services
builder.Services.AddAntiforgery();

// JWT Bearer authentication (reads JWT_PUBLIC_KEY env var — fails fast if absent)
builder.Services.AddAgendaBuddyAuthentication();
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

// HSTS (under its flag) and the HTTPS redirect run BEFORE authentication. Registered after
// UseAuthentication, the redirect parsed and validated the bearer token out of a plaintext
// request and only then told the client to come back over TLS.
app.UseAgendaBuddyTransportSecurity();

app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();
app.UseStatusCodePages();

var professions = app.MapGroup("api/v1/professions")
    .WithTags("ProfessionAPI")
    .WithOpenApi()
    .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

// ADR-025: POST /api/v1/professions was DELETED, not role-gated. There is no role to check for --
// Identity's allow-list is exactly {Provider, Customer} (AgendaBuddy.Identity/Program.cs:121) with no
// administrative tier, so the only implementable check would still let any self-registered provider
// write global reference data read by every user. Professions are SEEDED from
// Library/Data/ProfessionSeedData.cs and no shipped flow creates one, so nothing is lost. Verified live
// before removal: both a Provider AND a Customer token received 201 and wrote to the catalogue.
// AddProfessionCommand/AddProfessionCommandHandler were DELETED rather than migrated forward. No route
// ever reached them (this MapGroup has no POST), and the handler's own constructor took a
// `ProfessionEntity` as a per-instance argument with no matching DI registration anywhere, so it could
// never have been resolved even if a route existed. Pinned by ProfessionWriteRouteRemovedTest, which is
// unaffected -- it never referenced the deleted types.

professions.MapGet("",
    async Task<Results<Ok<DataResponse<List<ProfessionEntity>>>, NoContent>> (
        IMediator mediator,
        IDistributedCache cache,
        CancellationToken cancellationToken) =>
    {
        const string key = "professions";

        // A Fail result is mapped to null so CacheAside's "never cache a null"
        // rule (CacheAside.cs) keeps an empty catalogue from poisoning the cache.
        var professionCollection = await cache.GetOrCreateAsync(key, async token =>
        {
            var result = await mediator.Send(new GetProfessionsQuery(), token);
            return result.IsSuccess ? result.Value : null!;
        }, cancellationToken: cancellationToken);

        if (professionCollection is not null)
            return TypedResults.Ok(DataResponse<List<ProfessionEntity>>.Ok(professionCollection));

        return TypedResults.NoContent();
    }).WithName("GetProfessionList");

professions.MapGet("/{name}",
    async Task<Results<Ok<DataResponse<ProfessionEntity>>, NotFound>> (
        IMediator mediator,
        string name,
        IDistributedCache cache,
        CancellationToken cancellationToken) =>
    {
        var key = $"profession-{name}";

        var profession = await cache.GetOrCreateAsync(key, async token =>
        {
            var result = await mediator.Send(new GetProfessionByNameQuery { Name = name }, token);
            return result.IsSuccess ? result.Value : null!;
        }, cancellationToken: cancellationToken);

        if (profession is not null)
            return TypedResults.Ok(DataResponse<ProfessionEntity>.Ok(profession));

        return TypedResults.NotFound();
    }).WithName("GetProfessionByName");

app.Run();

// Functions and Methods
void CustomizeProblemDetails(ProblemDetails problemDetails, HttpContext httpContext)
{
    problemDetails.Extensions["requestId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
}
