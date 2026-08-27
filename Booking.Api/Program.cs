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
// F-019-T04: handlers moved to Booking.Core, a separate assembly from Booking.Api -- MediatR's
// RegisterServicesFromAssembly only scans the one assembly it's given, so both must be registered or
// mediator.Send(command) throws "no handler registered" at runtime, not at compile time.
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblies(typeof(Program).Assembly, typeof(BookingAppointmentCommandHandler).Assembly));
builder.Services.AddEventStore();

// Add services required to support using MVC's model binders
builder.Services.AddMvcCore();

// F-014: ObjectId has no JSON representation of its own, so System.Text.Json serialises the struct's public
// properties and emits `"id": { "timestamp": …, "machine": … }` — a shape that cannot be read back into an
// ObjectId at all. Three of F-014's route families need the id from a create response in order to work
// (PUT /notes/{id}, POST /messages/{id}/read, POST /notifications/{id}/read), so this is load-bearing rather
// than cosmetic. Pre-existing for every other route that returns an entity; see ObjectIdJsonConverter.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new ObjectIdJsonConverter()));

// F-019-T02 Validot spike: one shared, immutable, thread-safe IValidator<AppointmentEntity> for the
// POST /appointments route only. See Booking/Validation/AppointmentEntitySpecification.cs for what it
// enforces and why it's .Optional() rather than .Required().
builder.Services.AddSingleton<IValidator<AppointmentEntity>>(
    Validator.Factory.Create(AppointmentEntitySpecification.Spec));

// Register Singleton instances
builder.Services.AddSingleton<IKafkaClient, KafkaClient>();

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

// F-014 PRD risk R4: the residual risk of a non-charging default is that it becomes permanent — a deployment
// forgets the key and records payments that never happened while every artifact says F-010 is delivered. Same
// shape as threat T-103, same mitigation as ADR-033: say so loudly, do not refuse to start. A missing payment
// key must not take appointment booking offline.
if (PaymentGatewayFactory.RecordingModeWarning(
        app.Configuration, SecurityFlags.IsLocalRun(app.Configuration, app.Environment)) is { } paymentWarning)
{
    app.Logger.LogWarning("PAYMENTS NOT REAL — {Warning}", paymentWarning);
}

app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();
app.UseStatusCodePages();

var booking = app.MapGroup("api/v1/booking")
    .WithTags("BookingAPI")
    .WithOpenApi()
    .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

booking.MapPost("/appointments",
        async Task<Results<ValidationProblem, ForbidHttpResult, Created<DataResponse<AppointmentEntity>>, BadRequest<DataResponse<AppointmentEntity>>>> (
            IMediator mediator,
            ClaimsPrincipal user,
            AppointmentEntity appointmentEntity,
            IValidator<AppointmentEntity> appointmentValidator,
            CancellationToken cancellationToken) =>
        {
            // F-019-T02 Validot spike: this is the one route swapped from MiniValidator.TryValidate to
            // Validot for a real vertical-slice comparison. The other two original routes below (PUT,
            // DELETE) are deliberately untouched.
            var validationResult = appointmentValidator.Validate(appointmentEntity);
            if (validationResult.AnyErrors)
                return TypedResults.ValidationProblem(
                    validationResult.MessageMap.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()));

            // Either the provider or customer booking on behalf of themselves
            try { OwnershipGuard.AssertOwnerAny(user, appointmentEntity.EmailProvider, appointmentEntity.EmailCustomer); }
            catch (ForbiddenException) { return TypedResults.Forbid(); }

            var result = await mediator.Send(new BookAppointmentCommand { AppointmentEntity = appointmentEntity },
                cancellationToken);

            if (result.IsSuccess)
                return TypedResults.Created($"/api/v1/appointments/{appointmentEntity.Identifier}",
                    DataResponse<AppointmentEntity>.Ok(result.Value));
            return TypedResults.BadRequest(
                DataResponse<AppointmentEntity>.Fail(result.Errors.Select(e => e.Message)));
        })
    .WithName("BookAppointment")
    .RequireAuthorization();

booking.MapPut("/appointments/",
        async Task<Results<ValidationProblem, ForbidHttpResult, Accepted<DataResponse<AppointmentEntity>>, BadRequest<DataResponse<AppointmentEntity>>>> (
            IMediator mediator,
            ClaimsPrincipal user,
            AppointmentEntity appointmentEntity,
            CancellationToken cancellationToken) =>
        {
            if (!MiniValidator.TryValidate(appointmentEntity, out var errors))
                return TypedResults.ValidationProblem(errors);

            try { OwnershipGuard.AssertOwnerAny(user, appointmentEntity.EmailProvider, appointmentEntity.EmailCustomer); }
            catch (ForbiddenException) { return TypedResults.Forbid(); }

            var result = await mediator.Send(new UpdateAppointmentCommand { AppointmentEntity = appointmentEntity },
                cancellationToken);

            if (result.IsSuccess)
                return TypedResults.Accepted($"/api/v1/appointments/{appointmentEntity.Identifier}",
                    DataResponse<AppointmentEntity>.Ok(result.Value));
            return TypedResults.BadRequest(
                DataResponse<AppointmentEntity>.Fail(result.Errors.Select(e => e.Message)));
        })
    .WithName("UpdateAppointment")
    .RequireAuthorization();

booking.MapDelete("/appointments/",
        async Task<Results<ValidationProblem, ForbidHttpResult, NoContent, BadRequest<DataResponse<AppointmentEntity>>>> (
            IMediator mediator,
            ClaimsPrincipal user,
            [FromBody] AppointmentEntity appointmentEntity,
            CancellationToken cancellationToken) =>
        {
            if (!MiniValidator.TryValidate(appointmentEntity, out var errors))
                return TypedResults.ValidationProblem(errors);

            try { OwnershipGuard.AssertOwnerAny(user, appointmentEntity.EmailProvider, appointmentEntity.EmailCustomer); }
            catch (ForbiddenException) { return TypedResults.Forbid(); }

            var result = await mediator.Send(new CancelAppointmentCommand { Identifier = appointmentEntity.Identifier },
                cancellationToken);

            // A 204 cannot carry a body by HTTP semantics, so the success Value is discarded here rather
            // than forced into an envelope -- a disclosed exception to Requirement 10's blanket claim, not
            // a silent one. See CancelAppointmentCommandHandler's remarks and T11's final verification.
            if (result.IsSuccess) return TypedResults.NoContent();
            return TypedResults.BadRequest(
                DataResponse<AppointmentEntity>.Fail(result.Errors.Select(e => e.Message)));
        })
    .WithName("CancelAppointment")
    .RequireAuthorization();

// ── F-014: appointment status, session notes, payments ───────────────────────────────────────────────
//
const string ProviderRole = "Provider";
//
// Three route families that did not exist. Every one is authenticated, ownership-guarded, and role-checked
// where a role distinction exists — F-016 is the reason that is stated rather than assumed: five routes in
// this solution returned PII to anonymous callers, and the fix was a guard on every route.

// F-014 requirement 14 / threat T-203. Status is SERVER-OWNED: the PUT above ignores the field, and this is
// the only way to change it. The transition runs through AppointmentEntity.TransitionTo, so Book() and
// Complete() — dead code until now — hold the rules.
booking.MapPost("/appointments/{identifier}/status",
        async Task<Results<Ok<DataResponse<AppointmentStatusResponse>>, ForbidHttpResult, NotFound, Conflict<string>, BadRequest<string>>> (
            string identifier,
            ClaimsPrincipal user,
            AppointmentStatusRequest request,
            BookingService bookingService,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            // Enum.TryParse also accepts the NUMERIC form, and — less obviously — accepts undefined numbers:
            // TryParse<AppointmentStatus>("99") succeeds with the value 99. Enum.IsDefined is what turns that
            // into a 400 rather than letting it reach the transition and answer 409, which would imply the state
            // exists and merely conflicts.
            if (request is null
                || !Enum.TryParse<AppointmentStatus>(request.Status, ignoreCase: true, out var target)
                || !Enum.IsDefined(target))
            {
                return TypedResults.BadRequest("status must be one of: Booked, Completed.");
            }

            var appointment = await bookingService.SearchAppointmentAsync(identifier);
            if (appointment is null) return TypedResults.NotFound();

            // Either participant may book; only the provider may complete. A customer marking their own
            // session complete is a claim about work delivered, not a scheduling action.
            try
            {
                OwnershipGuard.AssertOwnerAny(user, appointment.EmailProvider, appointment.EmailCustomer);
                if (target == AppointmentStatus.Completed)
                    OwnershipGuard.AssertOwner(user, appointment.EmailProvider);
            }
            catch (ForbiddenException) { return TypedResults.Forbid(); }

            try
            {
                var result = await mediator.Send(
                    new ChangeAppointmentStatusCommand { Identifier = identifier, TargetStatus = target },
                    cancellationToken);

                if (result.IsFailed) return TypedResults.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                // The entity's own guard refused the transition. 409 rather than 400: the request is
                // well-formed, it conflicts with the current state.
                return TypedResults.Conflict(ex.Message);
            }

            return TypedResults.Ok(DataResponse<AppointmentStatusResponse>.Ok(
                new AppointmentStatusResponse(identifier, target.ToString())));
        })
    .WithName("ChangeAppointmentStatus")
    .RequireAuthorization();

// ── Session notes — the most sensitive data in the product ───────────────────────────────────────────
//
// Threat T-201: the owning provider is taken from the CALLER'S TOKEN and never from the request. NoteService
// asks for a providerEmail, and a route that passed a client-supplied one through would hand any
// authenticated caller every provider's notes for any appointment identifier they can guess — identifiers a
// customer already receives in their own appointment responses. That is F-016's defect exactly.
//
// Threat T-202: KeyNotFoundException and UnauthorizedAccessException BOTH map to 403, so a caller cannot
// tell "someone else's note" from "no such note". For a therapist, the existence of a note is itself
// disclosure.
booking.MapGet("/appointments/{identifier}/notes",
        async Task<Results<Ok<DataResponse<IEnumerable<NoteEntity>>>, ForbidHttpResult>> (
            string identifier, ClaimsPrincipal user, BookingService bookingService,
            IMediator mediator, CancellationToken cancellationToken) =>
        {
            var providerEmail = user.FindFirstValue(ClaimTypes.NameIdentifier);

            try
            {
                OwnershipGuard.AssertRole(user, ProviderRole);

                var appointment = await bookingService.SearchAppointmentAsync(identifier);
                // A missing appointment answers 403 alongside a foreign one: distinguishing them would turn
                // this route into an appointment-existence oracle for any authenticated provider.
                OwnershipGuard.AssertOwner(user, appointment?.EmailProvider);
            }
            catch (ForbiddenException) { return TypedResults.Forbid(); }

            var result = await mediator.Send(
                new GetAppointmentNotesQuery { ProviderEmail = providerEmail!, Identifier = identifier },
                cancellationToken);
            return TypedResults.Ok(DataResponse<IEnumerable<NoteEntity>>.Ok(result.Value));
        })
    .WithName("GetAppointmentNotes")
    .RequireAuthorization();

booking.MapPost("/appointments/{identifier}/notes",
        async Task<Results<Created<DataResponse<NoteEntity>>, ForbidHttpResult, BadRequest<string>>> (
            string identifier, ClaimsPrincipal user, NoteRequest request,
            BookingService bookingService, IMediator mediator, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request?.Content))
                return TypedResults.BadRequest("content is required.");

            var providerEmail = user.FindFirstValue(ClaimTypes.NameIdentifier);

            try
            {
                OwnershipGuard.AssertRole(user, ProviderRole);
                var appointment = await bookingService.SearchAppointmentAsync(identifier);
                OwnershipGuard.AssertOwner(user, appointment?.EmailProvider);
            }
            catch (ForbiddenException) { return TypedResults.Forbid(); }

            // providerEmail from the token, appointmentIdentifier from the path. A body carrying either is
            // ignored — NoteRequest has no such field, which is the cheapest way to guarantee it.
            var result = await mediator.Send(
                new CreateAppointmentNoteCommand
                {
                    ProviderEmail = providerEmail!,
                    Identifier = identifier,
                    Content = request.Content
                },
                cancellationToken);

            return TypedResults.Created($"/api/v1/booking/notes/{result.Value.Id}", DataResponse<NoteEntity>.Ok(result.Value));
        })
    .WithName("CreateAppointmentNote")
    .RequireAuthorization();

booking.MapPut("/notes/{id}",
        async Task<Results<Ok<DataResponse<NoteEntity>>, ForbidHttpResult, BadRequest<string>>> (
            string id, ClaimsPrincipal user, NoteRequest request,
            IMediator mediator, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request?.Content))
                return TypedResults.BadRequest("content is required.");

            var providerEmail = user.FindFirstValue(ClaimTypes.NameIdentifier);

            try
            {
                OwnershipGuard.AssertRole(user, ProviderRole);
                if (providerEmail is null) throw new ForbiddenException();

                var result = await mediator.Send(
                    new UpdateAppointmentNoteCommand { Id = id, ProviderEmail = providerEmail, Content = request.Content },
                    cancellationToken);
                return TypedResults.Ok(DataResponse<NoteEntity>.Ok(result.Value));
            }
            // Threat T-202: both causes answer the same way, deliberately.
            catch (ForbiddenException) { return TypedResults.Forbid(); }
            catch (UnauthorizedAccessException) { return TypedResults.Forbid(); }
            catch (KeyNotFoundException) { return TypedResults.Forbid(); }
        })
    .WithName("UpdateAppointmentNote")
    .RequireAuthorization();

booking.MapDelete("/notes/{id}",
        async Task<Results<NoContent, ForbidHttpResult>> (
            string id, ClaimsPrincipal user, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var providerEmail = user.FindFirstValue(ClaimTypes.NameIdentifier);

            try
            {
                OwnershipGuard.AssertRole(user, ProviderRole);
                if (providerEmail is null) throw new ForbiddenException();

                await mediator.Send(
                    new DeleteAppointmentNoteCommand { Id = id, ProviderEmail = providerEmail }, cancellationToken);
                // A 204 cannot carry a body by HTTP semantics -- same disclosed exception to
                // Requirement 10's blanket claim as Booking.Api's Cancel route (F-019-T04).
                return TypedResults.NoContent();
            }
            catch (ForbiddenException) { return TypedResults.Forbid(); }
            catch (UnauthorizedAccessException) { return TypedResults.Forbid(); }
            catch (KeyNotFoundException) { return TypedResults.Forbid(); }
        })
    .WithName("DeleteAppointmentNote")
    .RequireAuthorization();

// ── Payments ────────────────────────────────────────────────────────────────────────────────────────
//
// Threat T-205: both participant emails come from the STORED APPOINTMENT, never from the body, so a caller
// cannot record a payment against someone else. A second charge for the same appointment answers 409.
//
// ⚠️ RESIDUAL, ACCEPTED: `amount` is client-supplied and there is nothing to validate it against, because an
// appointment does not record which service it was booked for. With the default non-charging gateway a wrong
// amount corrupts a record; with a real Stripe key it would be a real underpayment. Anyone configuring
// Payments:Stripe:ApiKey must read threat T-205 first.
booking.MapPost("/appointments/{identifier}/payment",
        async Task<Results<Created<DataResponse<PaymentEntity>>, ForbidHttpResult, NotFound, Conflict<string>, BadRequest<string>>> (
            string identifier, ClaimsPrincipal user, PaymentRequest request,
            BookingService bookingService, IMediator mediator, CancellationToken cancellationToken) =>
        {
            if (request is null || request.Amount <= 0)
                return TypedResults.BadRequest("amount must be greater than zero.");

            var appointment = await bookingService.SearchAppointmentAsync(identifier);
            if (appointment is null) return TypedResults.NotFound();

            try { OwnershipGuard.AssertOwnerAny(user, appointment.EmailProvider, appointment.EmailCustomer); }
            catch (ForbiddenException) { return TypedResults.Forbid(); }

            try
            {
                var result = await mediator.Send(
                    new PayForAppointmentCommand
                    {
                        Identifier = identifier,
                        ProviderEmail = appointment.EmailProvider,
                        CustomerEmail = appointment.EmailCustomer,
                        Amount = request.Amount,
                        Currency = string.IsNullOrWhiteSpace(request.Currency) ? "usd" : request.Currency
                    },
                    cancellationToken);

                return TypedResults.Created(
                    $"/api/v1/booking/appointments/{identifier}/payment", DataResponse<PaymentEntity>.Ok(result.Value));
            }
            catch (InvalidOperationException ex)
            {
                // Threat T-205's Conflict case. 409 rather than 400: the request is well-formed, it
                // conflicts with the current state (already paid).
                return TypedResults.Conflict(ex.Message);
            }
        })
    .WithName("PayForAppointment")
    .RequireAuthorization();

booking.MapGet("/appointments/{identifier}/payment",
        async Task<Results<Ok<DataResponse<PaymentEntity>>, ForbidHttpResult, NotFound>> (
            string identifier, ClaimsPrincipal user,
            BookingService bookingService, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var appointment = await bookingService.SearchAppointmentAsync(identifier);
            if (appointment is null) return TypedResults.NotFound();

            try { OwnershipGuard.AssertOwnerAny(user, appointment.EmailProvider, appointment.EmailCustomer); }
            catch (ForbiddenException) { return TypedResults.Forbid(); }

            // 404 is safe here: the caller has already proven they are a participant in this appointment.
            var result = await mediator.Send(new GetAppointmentPaymentQuery { Identifier = identifier }, cancellationToken);
            return result.IsFailed ? TypedResults.NotFound() : TypedResults.Ok(DataResponse<PaymentEntity>.Ok(result.Value));
        })
    .WithName("GetAppointmentPayment")
    .RequireAuthorization();

app.Run();

// Functions and Methods
void CustomizeProblemDetails(ProblemDetails problemDetails, HttpContext httpContext)
{
    problemDetails.Extensions["requestId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
}
