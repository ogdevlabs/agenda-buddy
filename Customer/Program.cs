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

// Add MongoDB
builder.Services.AddMongoDbRepository(builder.Configuration);

// Add Distributed Cache
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

var customers = app.MapGroup("/api/v1/customers")
    .WithTags("CustomerAPI")
    .WithOpenApi()
    .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

customers.MapPost("/", async Task<Results<ValidationProblem, Created<CustomerEntity>>> (
    IMediator mediator,
    CustomerService customerService,
    CustomerEntity customerEntity,
    IRequestCollection requestCollection) =>
{
    if (!MiniValidator.TryValidate(customerEntity, out var errors))
        return TypedResults.ValidationProblem(errors);
    var filter =
        SupportTools<CustomerEntity>.FilterByNameAndLastName(customerEntity.FirstName!, customerEntity.LastName!);
    var existingCustomer = await customerService.FindCustomerAsync(filter);
    var topicName = KafkaHelper.CreateCustomerTopicName(customerEntity.Email!);
    if (existingCustomer != null)
        return TypedResults.ValidationProblem(GenerateErrorMessage(
            "Existing record found", new[]
            {
                $"Email:{customerEntity.Email}"
            }));

    var eventResponse =
        await EventsHelper.AddCustomerEvent(requestCollection, mediator, customerService, customerEntity);
    if (!string.IsNullOrEmpty(eventResponse) && !eventResponse.ToLower().StartsWith("exception"))
        return TypedResults.Created($"/api/v1/customers/{customerEntity.Id}", customerEntity);

    return TypedResults.ValidationProblem(GenerateErrorMessage(
        "Kafka Error", new[] { "Kafka Topic", $"{topicName}" })
    );
})
.WithName("CreateCustomer")
.RequireAuthorization();

customers.MapPut("/{email}",
    async Task<Results<ValidationProblem, ForbidHttpResult, NotFound, Accepted>> (string email,
        ClaimsPrincipal user,
        IMediator mediator,
        CustomerService customerService, CustomerEntity customerEntity, IRequestCollection requestCollection) =>
    {
        if (!MiniValidator.TryValidate(customerEntity, out var errors))
            return TypedResults.ValidationProblem(errors);

        try { OwnershipGuard.AssertOwner(user, email); }
        catch (ForbiddenException) { return TypedResults.Forbid(); }

        var eventResponse =
            await EventsHelper.UpdateCustomerEvent(email, requestCollection, mediator, customerService, customerEntity);

        if (!string.IsNullOrEmpty(eventResponse)) return TypedResults.Accepted("api/v1/customers");

        return TypedResults.NotFound();
    })
    .WithName("UpdateCustomer")
    .RequireAuthorization();

customers.MapGet("",
    async Task<Results<Ok<List<CustomerEntity>>, NoContent>> (IMediator mediator,
        CustomerService customerService, IRequestCollection requestCollection, IDistributedCache cache) =>
    {
        var key = $"customers";
        var customerCollection = await cache.GetOrCreateAsync(key,
            async token => await EventsHelper.GetCustomersEvent(requestCollection, mediator, customerService));

        if (customerCollection is not null)
            return TypedResults.Ok(customerCollection);

        return TypedResults.NoContent();
    }).WithName("GetAllCustomers");

customers.MapGet("/{email}", async Task<Results<Ok<CustomerEntity>, NotFound>> (IMediator mediator, string email,
    CustomerService customerService, IRequestCollection requestCollection, IDistributedCache cache) =>
{
    var key = $"customers-{email}";

    var customer = await cache.GetOrCreateAsync(key,
        async token => await EventsHelper.GetCustomerByEmailEvent(requestCollection, mediator, customerService, email));
    
    if (customer is not null)
        return TypedResults.Ok(customer);
    
    return TypedResults.NotFound();
}).WithName("GetCustomerByEmail");

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