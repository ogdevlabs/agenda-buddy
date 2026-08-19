using System.Security.Claims;
using Library.Tools;
using Microsoft.Extensions.Caching.Distributed;

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

// Add services to the container.
builder.Services.AddAuthorization();
// Add cache
builder.Services.AddDistributedMemoryCache();
// Add MongoDB
builder.Services.AddMongoDbRepository(builder.Configuration);
// Add MediatR
builder.Services.AddMediatR(cfg => { cfg.RegisterServicesFromAssembly(typeof(Program).Assembly); });
builder.Services.AddEventStore();
// Add services required to support using MVC's model binders
builder.Services.AddMvcCore();
// Register Singleton instances
// Scoped, not Singleton: RequestCollection consumes the scoped IEventStore, and a
// singleton capturing it fails DI validation — which is enabled in Development, the
// environment the AppHost runs services in. RequestCollection is stateless, so request
// scope is the correct lifetime rather than a workaround.
builder.Services.AddScoped<IRequestCollection, RequestCollection>();

// Enable & configure JSON Problem Details error responses
// ADR-022 / F-016-T08: ForbiddenException -> 403 centrally, so an endpoint that omits a local
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

app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();
app.UseStatusCodePages();
app.UseHttpsRedirection();

var calendar = app.MapGroup("api/v1/calendar")
    .WithTags("CalendarAPI")
    .WithOpenApi()
    .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

calendar.MapGet("/availability/{email}",
    async Task<Results<Ok<List<DateTime>>, NotFound>> (
        IMediator mediator,
        ClaimsPrincipal user,
        string email,
        ProviderService providerService,
        CalendarService calendarService,
        IRequestCollection requestCollection, IDistributedCache cache) =>
    {
        // F-016 AC-10 / requirement 11 / threat T-006. A valid token proves the caller is SOMEBODY, not
        // that {email} is theirs. Without this line any registered user could read any provider's full
        // appointment list, including every customer email in it. Every sibling service already guarded
        // (Provider:213, Customer:171, Services:153,:177); Calendar was the one family that forgot, and
        // nothing could catch it because there was no integration test in the solution
        // (11-testing.md:148).
        //
        // ⚠️ DESIGN INVARIANT, NOT AN IMPLEMENTATION DETAIL: this MUST stay ABOVE the cache read. The
        // cache key is derived from {email} -- the request SUBJECT -- never the CALLER, so a cached value
        // is not necessarily one the next caller may see. Ordering is the only thing that makes it safe.
        // Reordering these lines, extracting a helper, or caching the RESPONSE instead of the DATA creates
        // a cross-tenant leak, and F-019/F-020 will rewrite this exact file. Pinned by
        // CalendarOwnershipTest.T006_AWarmCacheIsNotServedToADifferentPrincipal.
        //
        // No local try/catch: T08's AgendaBuddyExceptionHandler maps ForbiddenException to 403 centrally.
        OwnershipGuard.AssertOwner(user, email);

        var key = $"availability-{email}";

        var dateTimesCollection = await cache.GetOrCreateAsync(key, async token =>
            await EventHelper.CheckCalendarAvailabilityEvent(requestCollection, mediator, providerService,
                calendarService,
                email));

        if (dateTimesCollection is null)
            return TypedResults.NotFound();

        var enumerable = dateTimesCollection.ToList();

        if (enumerable.Count != 0)
            return TypedResults.Ok(dateTimesCollection);

        return TypedResults.NotFound();
    })
    .WithName("CheckCalendarAvailability")
    .RequireAuthorization();

calendar.MapGet("/appointments/{email}",
    async Task<Results<Ok<List<AppointmentEntity>>, NotFound>> (
        IMediator mediator,
        ClaimsPrincipal user,
        string email,
        ProviderService providerService,
        CalendarService calendarService,
        IRequestCollection requestCollection, IDistributedCache cache) =>
    {
        // F-016 AC-10 / requirement 11 / threat T-006. A valid token proves the caller is SOMEBODY, not
        // that {email} is theirs. Without this line any registered user could read any provider's full
        // appointment list, including every customer email in it. Every sibling service already guarded
        // (Provider:213, Customer:171, Services:153,:177); Calendar was the one family that forgot, and
        // nothing could catch it because there was no integration test in the solution
        // (11-testing.md:148).
        //
        // ⚠️ DESIGN INVARIANT, NOT AN IMPLEMENTATION DETAIL: this MUST stay ABOVE the cache read. The
        // cache key is derived from {email} -- the request SUBJECT -- never the CALLER, so a cached value
        // is not necessarily one the next caller may see. Ordering is the only thing that makes it safe.
        // Reordering these lines, extracting a helper, or caching the RESPONSE instead of the DATA creates
        // a cross-tenant leak, and F-019/F-020 will rewrite this exact file. Pinned by
        // CalendarOwnershipTest.T006_AWarmCacheIsNotServedToADifferentPrincipal.
        //
        // No local try/catch: T08's AgendaBuddyExceptionHandler maps ForbiddenException to 403 centrally.
        OwnershipGuard.AssertOwner(user, email);

        var key = $"appointments-{email}";

        var appointmentEntities = await cache.GetOrCreateAsync(key, async token =>
            await EventHelper.CheckCalendarAppointmentsEvent(requestCollection, mediator, providerService,
                calendarService,
                email));

        if (appointmentEntities is not null) return TypedResults.Ok(appointmentEntities);

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