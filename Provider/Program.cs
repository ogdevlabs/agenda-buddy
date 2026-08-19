using Library.Dtos;
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

app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();
app.UseStatusCodePages();
app.UseHttpsRedirection();


var providers = app.MapGroup("/api/v1/providers")
    .WithTags("ProviderAPI")
    .WithOpenApi()
    .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

// Create a Provider, verifying for duplicate record
// create a Topic for the provider
providers.MapPost("/", async Task<Results<ValidationProblem, Created<ProviderEntity>>> (
        IMediator mediator,
        ClaimsPrincipal user,
        ProviderService providerService,
        ProviderEntity providerEntity,
        IRequestCollection requestCollection) =>
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
        // No local try/catch: T08's AgendaBuddyExceptionHandler maps ForbiddenException to 403 centrally.
        OwnershipGuard.AssertRole(user, "Provider");
        OwnershipGuard.AssertOwner(user, providerEntity.Email);
        var filter =
            SupportTools<ProviderEntity>.FilterByNameAndLastName(providerEntity.FirstName, providerEntity.LastName);
        var existingProvider = await providerService.FindProvidersAsync(filter);
        var topicName = KafkaHelper.CreateProviderTopicName(providerEntity.Email!);
        if (existingProvider is not null)
            return TypedResults.ValidationProblem(GenerateErrorMessage(
                "Existing record found", new[]
                {
                    $"Email:{providerEntity.Email}"
                }));

        var eventResponse =
            await EventsHelper.AddProviderEvent(requestCollection, mediator, providerService, providerEntity);
        if (!string.IsNullOrEmpty(eventResponse) && !eventResponse.ToLower().StartsWith("exception"))
            return TypedResults.Created($"/api/v1/providers/{providerEntity.Id}", providerEntity);

        return TypedResults.ValidationProblem(GenerateErrorMessage(
            "Kafka Error", new[] { "Kafka Topic", $"{topicName}" })
        );
    })
    .WithName("CreateProvider")
    .RequireAuthorization();

// Get provider list
providers.MapGet("", async Task<Ok<PagedResponse<ProviderSummary>>> (IMediator mediator,
    ProviderService providerService,
    IRequestCollection requestCollection, IDistributedCache cache,
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
        var listProviders =
            await EventsHelper.GetProvidersEvent(requestCollection, mediator, providerService, pageRequest);
        return listProviders;
    });

    if (providerCollection is null)
    {
        // 204 is RETIRED (ADR-023): a client always gets a parseable body. CacheAside returns default! on a
        // 500 ms lock timeout, so this branch is a cache miss rather than an empty collection.
        return TypedResults.Ok(PagedResponse<ProviderSummary>.From([], 0, pageRequest));
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
    return TypedResults.Ok(PagedResponse<ProviderSummary>.From(
        providerCollection.Items.Select(ProviderSummary.From).ToList(),
        providerCollection.TotalCount,
        pageRequest));
})
    // F-016 AC-8 / requirement 9: PII-bearing read, so no longer anonymous. Breaking change with zero reachable consumers (01-api-surface.md:158).
    .WithName("GetAllProviders")
    .RequireAuthorization();

// Get provider by Email
providers.MapGet("/{email}", async Task<Results<Ok<ProviderEntity>, Ok<ProviderSummary>, NotFound>> (
    IMediator mediator,
    ClaimsPrincipal user,
    string email,
    ProviderService providerService,
    IRequestCollection requestCollection, IDistributedCache cache) =>
{
    var key = $"providers-{email}";

    var providerEntity = await cache.GetOrCreateAsync(key, async token =>
    {
        var provider = await EventsHelper.GetProviderByEmail(requestCollection, mediator, providerService, email);
        return provider;
    });

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
        ? TypedResults.Ok(providerEntity)
        : TypedResults.Ok(ProviderSummary.From(providerEntity));
})
    // F-016 AC-8 / requirement 9: PII-bearing read, so no longer anonymous. Breaking change with zero reachable consumers (01-api-surface.md:158).
    .WithName("GetProviderByEmail")
    .RequireAuthorization();


// Update a provider, using email for search of the record
providers.MapPut("/{email}", async Task<Results<ValidationProblem, ForbidHttpResult, NotFound, Accepted>> (
    string email,
    ClaimsPrincipal user,
    IMediator mediator,
    ProviderService providerService,
    ProviderEntity providerEntity,
    IRequestCollection requestCollection) =>
{
    if (!MiniValidator.TryValidate(providerEntity, out var errors))
        return TypedResults.ValidationProblem(errors);

    try { OwnershipGuard.AssertOwner(user, email); }
    catch (ForbiddenException) { return TypedResults.Forbid(); }

    var eventResponse =
        await EventsHelper.UpdateProviderEvent(email, requestCollection, mediator, providerService, providerEntity);

    if (!string.IsNullOrEmpty(eventResponse)) return TypedResults.Accepted("api/v1/providers");

    return TypedResults.NotFound();
})
.WithName("UpdateProvider")
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