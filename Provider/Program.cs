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
        ProviderService providerService,
        ProviderEntity providerEntity,
        IRequestCollection requestCollection) =>
    {
        if (!MiniValidator.TryValidate(providerEntity, out var errors))
            return TypedResults.ValidationProblem(errors);
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
providers.MapGet("", async Task<Results<Ok<List<ProviderEntity>>, NoContent>> (IMediator mediator,
    ProviderService providerService,
    IRequestCollection requestCollection, IDistributedCache cache) =>
{
    var key = $"providers";
    var providerCollection = await cache.GetOrCreateAsync(key, async token =>
    {
        var listProviders = await EventsHelper.GetProvidersEvent(requestCollection, mediator, providerService);
        return listProviders;
    });

    if (providerCollection is not null)
        return TypedResults.Ok(providerCollection);

    return TypedResults.NoContent();
}).WithName("GetAllProviders");

// Get provider by Email
providers.MapGet("/{email}", async Task<Results<Ok<ProviderEntity>, NotFound>> (IMediator mediator,
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

    if (providerEntity is not null)
        return TypedResults.Ok(providerEntity);

    return TypedResults.NotFound();
}).WithName("GetProviderByEmail");


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