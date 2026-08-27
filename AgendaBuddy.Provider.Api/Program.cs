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

// Add distributed cache
builder.Services.AddDistributedMemoryCache();

// Add MediatR
// F-020-T11: handlers moved to AgendaBuddy.Provider.Core, a separate assembly from
// AgendaBuddy.Provider.Api -- MediatR's RegisterServicesFromAssembly only scans the one assembly it's
// given, so both must be registered or mediator.Send(command/query) throws "no handler registered" at
// runtime, not at compile time.
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblies(typeof(Program).Assembly, typeof(GetProvidersQueryHandler).Assembly));
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

app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();
app.UseStatusCodePages();


var providers = app.MapGroup("/api/v1/providers")
    .WithTags("ProviderAPI")
    .WithOpenApi()
    .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

// Create a Provider, verifying for duplicate record
// create a Topic for the provider
providers.MapPost("/", async Task<Results<ValidationProblem, Created<DataResponse<ProviderEntity>>>> (
        IMediator mediator,
        ClaimsPrincipal user,
        ProviderEntity providerEntity,
        CancellationToken cancellationToken) =>
    {
        if (!MiniValidator.TryValidate(providerEntity, out var errors))
            return TypedResults.ValidationProblem(errors);

        // F-016 AC-11 -- BOTH arms are required. A role check alone still lets one Provider create a
        // record under another provider's email, which is account takeover by registration. An ownership
        // check alone would let a Customer create provider records for themselves.
        //
        // This is one of only two AssertRole call sites in the solution after F-016. Per
        // 13-security.md:137 AssertRole had never been called anywhere, so the `role` claim authorized
        // nothing at all before this feature.
        //
        // F-020-T11: unchanged by the move -- confirmed both arms survive intact.
        // No local try/catch: T08's AgendaBuddyExceptionHandler maps ForbiddenException to 403 centrally.
        OwnershipGuard.AssertRole(user, "Provider");
        OwnershipGuard.AssertOwner(user, providerEntity.Email);

        // F-020-T11: dispatched through the real mediator.Send with the real request CancellationToken --
        // the pre-refactor path (Requests/RequestCollection.cs, deleted) manually `new`-ed the command
        // handler and called .Handle() directly. The duplicate-name check and Kafka topic creation both
        // moved into AddProviderCommandHandler, so this route is endpoint/DI wiring only.
        var result = await mediator.Send(new AddProviderCommand { ProviderEntity = providerEntity }, cancellationToken);

        if (result.IsSuccess)
            return TypedResults.Created($"/api/v1/providers/{providerEntity.Id}", DataResponse<ProviderEntity>.Ok(result.Value));

        return TypedResults.ValidationProblem(GenerateErrorMessage(
            "Provider Registration Error", result.Errors.Select(e => e.Message).ToArray()));
    })
    .WithName("CreateProvider")
    .RequireAuthorization();

// Get provider list
providers.MapGet("", async Task<Ok<DataResponse<PagedResponse<ProviderSummary>>>> (
    IMediator mediator,
    IDistributedCache cache,
    CancellationToken cancellationToken,
    int? page = null, int? pageSize = null) =>
{
    // F-016 AC-15 / ADR-023. Clamped, never rejected: a 400 would tell an attacker the exact boundary and
    // leave an honest client no way to discover the cap. MaxPageSize is a SECURITY control -- an uncapped
    // page size restores the full-dataset dump this feature exists to remove.
    var pageRequest = PageRequest.Clamp(page, pageSize);

    // ⚠️ The cache key carries the page, or page 2 would serve page 1's entry. Cheap to get wrong and
    // invisible in a single-page test.
    var key = $"providers-p{pageRequest.Page}-s{pageRequest.PageSize}";
    var providerCollection = await cache.GetOrCreateAsync(key, async token =>
    {
        var result = await mediator.Send(new GetProvidersQuery { Page = pageRequest }, token);
        return result.IsSuccess ? result.Value : null!;
    }, cancellationToken: cancellationToken);

    if (providerCollection is null)
    {
        // 204 is RETIRED (ADR-023): a client always gets a parseable body. CacheAside returns default! on a
        // 500 ms lock timeout, so this branch is a cache miss rather than an empty collection.
        return TypedResults.Ok(DataResponse<PagedResponse<ProviderSummary>>.Ok(
            PagedResponse<ProviderSummary>.From([], 0, pageRequest)));
    }

    // F-016 AC-9 / requirement 10. ProviderEntity embeds AppointmentEntities (each carrying
    // email_customer) and SubscribedCustomerCollection, so authentication alone does not fix this: an
    // authenticated CUSTOMER browsing for a coach would still receive every provider's appointment book
    // and client roster.
    //
    // ⚠️ THE LIST IS HOMOGENEOUS -- every element is a ProviderSummary, including the caller's own record.
    // api-contracts.md section 5.1 describes owner-gets-full for this route too, which would make `items`
    // a MIXED array of two shapes. That is not deserialisable into a typed list, and F-015 is written
    // against this contract. An owner loses nothing: GET /api/v1/providers/{email} returns their full
    // record, and that route DOES apply the ownership branch. Deviation recorded in api-contracts.md.
    return TypedResults.Ok(DataResponse<PagedResponse<ProviderSummary>>.Ok(PagedResponse<ProviderSummary>.From(
        providerCollection.Items.Select(ProviderSummary.From).ToList(),
        providerCollection.TotalCount,
        pageRequest)));
})
    // F-016 AC-8 / requirement 9: PII-bearing read, so no longer anonymous. Breaking change with zero reachable consumers (01-api-surface.md:158).
    .WithName("GetAllProviders")
    .RequireAuthorization();

// Get provider by Email
providers.MapGet("/{email}", async Task<Results<Ok<DataResponse<ProviderEntity>>, Ok<DataResponse<ProviderSummary>>, NotFound>> (
    IMediator mediator,
    ClaimsPrincipal user,
    string email,
    IDistributedCache cache,
    CancellationToken cancellationToken) =>
{
    var key = $"providers-{email}";

    var providerEntity = await cache.GetOrCreateAsync(key, async token =>
    {
        var result = await mediator.Send(new GetProviderByEmailQuery { Email = email }, token);
        return result.IsSuccess ? result.Value : null!;
    }, cancellationToken: cancellationToken);

    if (providerEntity is null)
        return TypedResults.NotFound();

    // F-016 AC-9 / requirement 10: two shapes, selected by ownership. Deliberately NOT 403 for a provider
    // you do not own -- reading another provider's SUMMARY is the discovery flow F-003 defines. Only the
    // embedded data is withheld.
    //
    // ⚠️ This branch is exactly why F-016-T09 had to land first (threat T-001). AssertOwner's null-claim
    // fall-through used to land on the OWNER side, so a token carrying no `sub` would have received the
    // unprojected entity. Pinned by ProviderProjectionTest.T001_*.
    // IsOwner rather than catching AssertOwner's ForbiddenException: "not the owner" selects a narrower
    // shape here, it is not a failure, and exception-driven control flow on a read path is both slower and
    // misleading. Both share one implementation, so the null-claim rule cannot drift between them.
    return OwnershipGuard.IsOwner(user, providerEntity.Email)
        ? TypedResults.Ok(DataResponse<ProviderEntity>.Ok(providerEntity))
        : TypedResults.Ok(DataResponse<ProviderSummary>.Ok(ProviderSummary.From(providerEntity)));
})
    // F-016 AC-8 / requirement 9: PII-bearing read, so no longer anonymous. Breaking change with zero reachable consumers (01-api-surface.md:158).
    .WithName("GetProviderByEmail")
    .RequireAuthorization();


// Update a provider, using email for search of the record
providers.MapPut("/{email}", async Task<Results<ValidationProblem, ForbidHttpResult, NotFound, Accepted<DataResponse<ProviderEntity>>>> (
    string email,
    ClaimsPrincipal user,
    IMediator mediator,
    ProviderEntity providerEntity,
    CancellationToken cancellationToken) =>
{
    if (!MiniValidator.TryValidate(providerEntity, out var errors))
        return TypedResults.ValidationProblem(errors);

    try { OwnershipGuard.AssertOwner(user, email); }
    catch (ForbiddenException) { return TypedResults.Forbid(); }

    var result = await mediator.Send(new UpdateProviderCommand { Email = email, ProviderEntity = providerEntity }, cancellationToken);

    if (result.IsSuccess)
        return TypedResults.Accepted("api/v1/providers", DataResponse<ProviderEntity>.Ok(result.Value));

    return TypedResults.NotFound();
})
.WithName("UpdateProvider")
.RequireAuthorization();

// ── F-014: reporting and deactivation ────────────────────────────────────────────────────────────────

// A provider's own metrics. {email} is in the path for symmetry with the other provider routes, NOT as a
// selector — it must equal the caller's own claim, so there is nothing to enumerate.
//
// ⚠️ The report carries NO revenue figure, deliberately (requirement 18). The old formula was completed
// appointments × the whole service catalogue's fees, and it cannot be corrected by arithmetic because an
// appointment does not record which service it was booked for. `revenueAvailable: false` plus a reason,
// rather than a plausible number that would be believed.
//
// F-020-T11: deliberately NOT wrapped in DataResponse<T>, unlike every other route in this file. This
// route never went through MediatR/Result<T> -- it calls IReportingService directly, both before and
// after this task -- and ReportAndDeactivationTest deserialises the body at the root
// (ReadFromJsonAsync<ProviderReport>, and a root-level "revenueAvailable"/"revenueUnavailableReason").
// Wrapping it would be a real behaviour change this task's recipe does not ask for. See
// AgendaBuddy.Provider.Domain.Responses.DataResponse's own remarks.
providers.MapGet("/{email}/report",
        async Task<Results<Ok<ProviderReport>, ForbidHttpResult, NotFound>> (
            string email, ClaimsPrincipal user, IReportingService reporting) =>
        {
            try
            {
                OwnershipGuard.AssertRole(user, "Provider");
                OwnershipGuard.AssertOwner(user, email);

                return TypedResults.Ok(await reporting.GetProviderReportAsync(email));
            }
            catch (ForbiddenException) { return TypedResults.Forbid(); }
            // Safe: the caller has already proven the path email is their own claim, so this can only mean
            // their own provider record is missing.
            catch (KeyNotFoundException) { return TypedResults.NotFound(); }
        })
    .WithName("GetProviderReport")
    .RequireAuthorization();

// Threat T-207: a provider deactivates THEMSELVES. Role plus ownership, and no administrative bypass —
// because there is no administrative role in this product (Identity's allow-list is exactly
// {Provider, Customer}, ADR-025), so there is nobody else who could legitimately call this. An unguarded
// version would let anyone take a business offline.
providers.MapPost("/{email}/deactivate",
        async Task<Results<Accepted<DataResponse<ProviderEntity>>, ForbidHttpResult, NotFound>> (
            string email,
            ClaimsPrincipal user,
            IMediator mediator,
            IProviderService providerService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                OwnershipGuard.AssertRole(user, "Provider");
                OwnershipGuard.AssertOwner(user, email);
            }
            catch (ForbiddenException) { return TypedResults.Forbid(); }

            var existing = await providerService.FindProvidersAsync(
                SupportTools<ProviderEntity>.FilterByEmail(email));
            if (existing is null) return TypedResults.NotFound();

            // F-020-T11: real mediator.Send dispatch. Previously the ONLY caller of
            // DeactivateProviderCommandHandler `new`-ed it directly and called .Handle() by hand
            // (Provider/Program.cs, deleted) -- this is the first time the handler is actually registered
            // with MediatR.
            var result = await mediator.Send(new DeactivateProviderCommand { ProviderEntity = existing }, cancellationToken);

            return result.IsSuccess
                ? TypedResults.Accepted($"/api/v1/providers/{email}", DataResponse<ProviderEntity>.Ok(result.Value))
                : TypedResults.NotFound();
        })
    .WithName("DeactivateProvider")
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
