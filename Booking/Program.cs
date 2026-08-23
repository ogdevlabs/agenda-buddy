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

// Add MongoDB
builder.Services.AddMongoDbRepository(builder.Configuration);

// Add MediatR
builder.Services.AddMediatR(cfg => { cfg.RegisterServicesFromAssembly(typeof(Program).Assembly); });
builder.Services.AddEventStore();

// Add services required to support using MVC's model binders
builder.Services.AddMvcCore();

// Register Singleton instances
builder.Services.AddSingleton<IKafkaClient, KafkaClient>();
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
    options.CustomizeProblemDetails = context => CustomizeProblemDetails(context.ProblemDetails, context.HttpContext));

// Add Anti-CSRF/XSRF services
builder.Services.AddAntiforgery();

// JWT Bearer authentication (reads JWT_PUBLIC_KEY env var — fails fast if absent)
builder.Services.AddAgendaBuddyAuthentication();
builder.Services.AddAuthorization();

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

// F-021 PRD requirement 13: HSTS (under its flag) and the HTTPS redirect run BEFORE authentication.
// Registered after UseAuthentication, as it was until F-021, the redirect parsed and validated the
// bearer token out of a plaintext request and only then told the client to come back over TLS.
app.UseAgendaBuddyTransportSecurity();

app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();
app.UseStatusCodePages();

var booking = app.MapGroup("api/v1/booking")
    .WithTags("BookingAPI")
    .WithOpenApi()
    .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

booking.MapPost("/appointments",
        async Task<Results<ValidationProblem, ForbidHttpResult, Created<AppointmentEntity>, BadRequest>> (
            IMediator mediator,
            ClaimsPrincipal user,
            ProviderService providerService, BookingService bookingService, AppointmentEntity appointmentEntity,
            IRequestCollection requestCollection) =>
        {
            if (!MiniValidator.TryValidate(appointmentEntity, out var errors))
                return TypedResults.ValidationProblem(errors);

            // Either the provider or customer booking on behalf of themselves
            try { OwnershipGuard.AssertOwnerAny(user, appointmentEntity.EmailProvider, appointmentEntity.EmailCustomer); }
            catch (ForbiddenException) { return TypedResults.Forbid(); }

            var eventResponse = await EventsHelper.BookAppointmentEvent(requestCollection, mediator, providerService,
                bookingService, appointmentEntity);

            if (!string.IsNullOrEmpty(eventResponse) && !eventResponse.ToLower().StartsWith("exception"))
                return TypedResults.Created($"/api/v1/appointments/{appointmentEntity.Identifier}", appointmentEntity);
            return TypedResults.ValidationProblem(GenerateErrorMessage(
                "No record match found error", new[] { "No provider", $"{appointmentEntity.EmailProvider}" }));
        })
    .WithName("BookAppointment")
    .RequireAuthorization();

booking.MapPut("/appointments/",
        async Task<Results<ValidationProblem, ForbidHttpResult, Accepted<AppointmentEntity>, BadRequest>> (
            IMediator mediator,
            ClaimsPrincipal user,
            ProviderService providerService, BookingService bookingService, AppointmentEntity appointmentEntity,
            IRequestCollection requestCollection) =>
        {
            if (!MiniValidator.TryValidate(appointmentEntity, out var errors))
                return TypedResults.ValidationProblem(errors);

            try { OwnershipGuard.AssertOwnerAny(user, appointmentEntity.EmailProvider, appointmentEntity.EmailCustomer); }
            catch (ForbiddenException) { return TypedResults.Forbid(); }

            var eventResponse = await EventsHelper.UpdateAppointmentEvent(requestCollection, mediator, providerService,
                bookingService, appointmentEntity);

            if (!string.IsNullOrEmpty(eventResponse) && !eventResponse.ToLower().StartsWith("exception"))
                return TypedResults.Accepted($"/api/v1/appointments/{appointmentEntity.Identifier}", appointmentEntity);
            return TypedResults.ValidationProblem(GenerateErrorMessage(
                "Update Appointment Error",
                new[] { "Error when trying to update appointment identifier:", $"{appointmentEntity.Identifier}" }));
        })
    .WithName("UpdateAppointment")
    .RequireAuthorization();

booking.MapDelete("/appointments/",
        async Task<Results<ValidationProblem, ForbidHttpResult, NoContent, BadRequest>> (IMediator mediator,
            ClaimsPrincipal user,
            ProviderService providerService, BookingService bookingService,
            [FromBody] AppointmentEntity appointmentEntity,
            IRequestCollection requestCollection) =>
        {
            if (!MiniValidator.TryValidate(appointmentEntity, out var errors))
                return TypedResults.ValidationProblem(errors);

            try { OwnershipGuard.AssertOwnerAny(user, appointmentEntity.EmailProvider, appointmentEntity.EmailCustomer); }
            catch (ForbiddenException) { return TypedResults.Forbid(); }

            var eventResponse = await EventsHelper.CancelAppointmentEvent(requestCollection, mediator, providerService,
                bookingService, appointmentEntity);

            if (!string.IsNullOrEmpty(eventResponse) && !eventResponse.ToLower().StartsWith("exception"))
                return TypedResults.NoContent();
            return TypedResults.ValidationProblem(GenerateErrorMessage(
                "Cancel Appointment Error",
                new[] { "Error when trying to cancel appointment identifier:", $"{appointmentEntity.Identifier}" }));
        })
    .WithName("CancelAppointment")
    .RequireAuthorization();

app.Run();

// Functions and Methods
void CustomizeProblemDetails(ProblemDetails problemDetails, HttpContext httpContext)
{
    problemDetails.Extensions["requestId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
}

Dictionary<string, string[]> GenerateErrorMessage(string key, string[] values)
{
    var dictionary = new Dictionary<string, string[]> { { key, values } };
    return dictionary;
}