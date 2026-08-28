using AgendaBuddy.Provider.Api.Modules;
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

// Add distributed cache
builder.Services.AddDistributedMemoryCache();

// Add MediatR
// Handlers live in AgendaBuddy.Provider.Core, a separate assembly from AgendaBuddy.Provider.Api --
// MediatR's RegisterServicesFromAssembly only scans the one assembly it's given, so both must be
// registered or mediator.Send(command/query) throws "no handler registered" at runtime, not at compile
// time.
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblies(typeof(Program).Assembly, typeof(GetProvidersQueryHandler).Assembly));
builder.Services.AddEventStore();

// Add services required to support using MVC's model binders
builder.Services.AddMvcCore();

// ObjectId has no JSON representation of its own, so System.Text.Json serialises the struct's public
// properties and emits `"id": { "timestamp": …, "machine": … }` — a shape that cannot be read back into an
// ObjectId at all. Some route families need the id from a create response in order to work
// (PUT /notes/{id}, POST /messages/{id}/read, POST /notifications/{id}/read), so this is load-bearing rather
// than cosmetic. Pre-existing for every other route that returns an entity; see ObjectIdJsonConverter.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new ObjectIdJsonConverter()));

// Register Singleton instances
builder.Services.AddSingleton<IKafkaClient, KafkaClient>();

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

builder.Services.AddCarter(configurator: c => c.WithModule<ProviderModule>());

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

// Same fire-and-forget rationale as above (F-024). IEventStore is scoped (it needs the
// request's principal to stamp Event.Actor), so ensuring its index at startup -- outside any
// request -- needs its own scope rather than resolving it straight from the root provider.
_ = Task.Run(async () =>
{
    try
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IEventStore>().EnsureIndexAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not ensure the events collection's retention index at startup");
    }
});

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

app.MapCarter();

app.Run();


// Functions and Methods
void CustomizeProblemDetails(ProblemDetails problemDetails, HttpContext httpContext)
{
    problemDetails.Extensions["requestId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
}
