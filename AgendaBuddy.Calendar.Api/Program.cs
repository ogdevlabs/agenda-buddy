using AgendaBuddy.Library.Tools;
using Microsoft.Extensions.Caching.Distributed;

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

// Add services to the container.
builder.Services.AddAuthorization();
// Add cache
builder.Services.AddDistributedMemoryCache();
// Add MongoDB
builder.Services.AddMongoDbRepository(builder.Configuration);

// Add MediatR
// Handlers live in AgendaBuddy.Calendar.Core, a separate assembly from AgendaBuddy.Calendar.Api --
// MediatR's RegisterServicesFromAssembly only scans the one assembly it's given, so both must be registered
// or mediator.Send(query) throws "no handler registered" at runtime, not at compile time.
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblies(typeof(Program).Assembly, typeof(CheckCalendarAvailabilityQueryHandler).Assembly));
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

// HSTS (under its flag) and the HTTPS redirect run BEFORE authentication. Registered after
// UseAuthentication, the redirect would parse and validate the bearer token out of a plaintext
// request and only then tell the client to come back over TLS.
app.UseAgendaBuddyTransportSecurity();

app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();
app.UseStatusCodePages();

var calendar = app.MapGroup("api/v1/calendar")
    .WithTags("CalendarAPI")
    .WithOpenApi()
    .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

calendar.MapGet("/availability/{email}",
    async Task<Results<Ok<DataResponse<List<DateTime>>>, NotFound>> (
        IMediator mediator,
        ClaimsPrincipal user,
        string email,
        IDistributedCache cache,
        CancellationToken cancellationToken) =>
    {
        // A valid token proves the caller is SOMEBODY, not that {email} is theirs. Without this line
        // any registered user could read any provider's full appointment list, including every
        // customer email in it.
        //
        // ⚠️ DESIGN INVARIANT, NOT AN IMPLEMENTATION DETAIL: this MUST stay ABOVE the cache read. The
        // cache key is derived from {email} -- the request SUBJECT -- never the CALLER, so a cached value
        // is not necessarily one the next caller may see. Ordering is the only thing that makes it safe.
        // Reordering these lines, extracting a helper, or caching the RESPONSE instead of the DATA creates
        // a cross-tenant leak. Pinned by CalendarOwnershipTest.T006_AWarmCacheIsNotServedToADifferentPrincipal.
        //
        // No local try/catch: AgendaBuddyExceptionHandler maps ForbiddenException to 403 centrally.
        OwnershipGuard.AssertOwner(user, email);

        var key = $"availability-{email}";

        // Dispatched through the real mediator.Send with the real request CancellationToken. A Fail
        // result is mapped to null so CacheAside's "never cache a null" rule (CacheAside.cs) keeps a
        // missing provider from poisoning the cache.
        var slots = await cache.GetOrCreateAsync(key, async token =>
        {
            var result = await mediator.Send(new CheckCalendarAvailabilityQuery { Email = email }, token);
            return result.IsSuccess ? result.Value : null!;
        }, cancellationToken: cancellationToken);

        // Unlike the appointments route below, an empty slot list answers 404 here too -- this mirrors the
        // route's pre-existing behaviour (Calendar/Program.cs before this task), not a new rule.
        if (slots is null || slots.Count == 0)
            return TypedResults.NotFound();

        return TypedResults.Ok(DataResponse<List<DateTime>>.Ok(slots));
    })
    .WithName("CheckCalendarAvailability")
    .RequireAuthorization();

calendar.MapGet("/appointments/{email}",
    async Task<Results<Ok<DataResponse<List<AppointmentEntity>>>, NotFound>> (
        IMediator mediator,
        ClaimsPrincipal user,
        string email,
        IDistributedCache cache,
        CancellationToken cancellationToken) =>
    {
        // A valid token proves the caller is SOMEBODY, not that {email} is theirs. Without this line
        // any registered user could read any provider's full appointment list, including every
        // customer email in it.
        //
        // ⚠️ DESIGN INVARIANT, NOT AN IMPLEMENTATION DETAIL: this MUST stay ABOVE the cache read. The
        // cache key is derived from {email} -- the request SUBJECT -- never the CALLER, so a cached value
        // is not necessarily one the next caller may see. Ordering is the only thing that makes it safe.
        // Reordering these lines, extracting a helper, or caching the RESPONSE instead of the DATA creates
        // a cross-tenant leak. Pinned by CalendarOwnershipTest.T006_AWarmCacheIsNotServedToADifferentPrincipal.
        //
        // No local try/catch: AgendaBuddyExceptionHandler maps ForbiddenException to 403 centrally.
        OwnershipGuard.AssertOwner(user, email);

        var key = $"appointments-{email}";

        var appointmentEntities = await cache.GetOrCreateAsync(key, async token =>
        {
            var result = await mediator.Send(new CheckCalendarAppointmentsQuery { Email = email }, token);
            return result.IsSuccess ? result.Value : null!;
        }, cancellationToken: cancellationToken);

        // Unlike availability above, an empty (but non-null) appointment list is a valid 200 -- a provider
        // with no appointments is not "not found". This mirrors the route's pre-existing behaviour
        // (Calendar/Program.cs before this task), not a new rule.
        if (appointmentEntities is not null) return TypedResults.Ok(DataResponse<List<AppointmentEntity>>.Ok(appointmentEntities));

        return TypedResults.NotFound();
    })
    .WithName("CheckCalendarAppointments")
    .RequireAuthorization();

app.Run();

// Functions and Methods
void CustomizeProblemDetails(ProblemDetails problemDetails, HttpContext httpContext)
{
    problemDetails.Extensions["requestId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
}
