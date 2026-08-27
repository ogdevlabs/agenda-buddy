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
// Handlers live in AgendaBuddy.Customer.Core, a separate assembly from AgendaBuddy.Customer.Api --
// MediatR's RegisterServicesFromAssembly only scans the one assembly it's given, so both must be
// registered or mediator.Send(command/query) throws "no handler registered" at runtime, not at
// compile time.
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblies(typeof(Program).Assembly, typeof(GetCustomersQueryHandler).Assembly));
builder.Services.AddEventStore();

// Add services required to support using MVC's model binders
builder.Services.AddMvcCore();

// ObjectId has no JSON representation of its own, so System.Text.Json serialises the struct's public
// properties and emits `"id": { "timestamp": …, "machine": … }` — a shape that cannot be read back into an
// ObjectId at all. Several route families need the id from a create response in order to work
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

var customers = app.MapGroup("/api/v1/customers")
    .WithTags("CustomerAPI")
    .WithOpenApi()
    .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

// Create a Customer, verifying for duplicate record, create a Kafka topic for the customer.
customers.MapPost("/", async Task<Results<ValidationProblem, Created<DataResponse<CustomerEntity>>>> (
    IMediator mediator,
    CustomerEntity customerEntity,
    CancellationToken cancellationToken) =>
{
    if (!MiniValidator.TryValidate(customerEntity, out var errors))
        return TypedResults.ValidationProblem(errors);

    // The duplicate-email check and Kafka topic creation both live in AddCustomerCommandHandler, so
    // this route is endpoint/DI wiring only.
    var result = await mediator.Send(new AddCustomerCommand { CustomerEntity = customerEntity }, cancellationToken);

    if (result.IsSuccess)
        return TypedResults.Created($"/api/v1/customers/{customerEntity.Id}", DataResponse<CustomerEntity>.Ok(result.Value));

    return TypedResults.ValidationProblem(GenerateErrorMessage(
        "Customer Registration Error", result.Errors.Select(e => e.Message).ToArray()));
})
.WithName("CreateCustomer")
.RequireAuthorization();

customers.MapPut("/{email}",
    async Task<Results<ValidationProblem, ForbidHttpResult, NotFound, Accepted<DataResponse<CustomerEntity>>>> (
        string email,
        ClaimsPrincipal user,
        IMediator mediator,
        CustomerEntity customerEntity,
        CancellationToken cancellationToken) =>
    {
        if (!MiniValidator.TryValidate(customerEntity, out var errors))
            return TypedResults.ValidationProblem(errors);

        // Deliberately NOT wrapped in try/catch. This is the route that demonstrates the central
        // mapping: AgendaBuddyExceptionHandler turns ForbiddenException into 403 whether or not an
        // endpoint remembered to catch it. ForbidHttpResult stays in the union above on purpose: this
        // route still returns 403, so removing it would drop 403 from the generated OpenAPI while the
        // behaviour was unchanged.
        OwnershipGuard.AssertOwner(user, email);

        var result = await mediator.Send(new UpdateCustomerCommand { Email = email, CustomerEntity = customerEntity }, cancellationToken);

        if (result.IsSuccess)
            return TypedResults.Accepted("api/v1/customers", DataResponse<CustomerEntity>.Ok(result.Value));

        return TypedResults.NotFound();
    })
    .WithName("UpdateCustomer")
    .RequireAuthorization();

customers.MapGet("",
    async Task<Ok<DataResponse<PagedResponse<CustomerEntity>>>> (
        IMediator mediator,
        ClaimsPrincipal user,
        IDistributedCache cache,
        CancellationToken cancellationToken,
        int? page = null, int? pageSize = null) =>
    {
        // ADR-026: the Provider role, not merely a token. Authenticating this route alone was nearly
        // worthless -- POST /api/v1/auth/register is anonymous, unverified and unrate-limited, so an
        // attacker self-registers as a Customer and pages the whole customer table exactly as before.
        // Pagination bounds the response, not the extraction.
        //
        // No local try/catch: AgendaBuddyExceptionHandler maps ForbiddenException to 403 centrally.
        // Guard runs BEFORE the cache read, so a refused caller never reaches cached data.
        OwnershipGuard.AssertRole(user, "Provider");

        // ADR-023. See Provider/Program.cs for why clamping rather than rejecting.
        var pageRequest = PageRequest.Clamp(page, pageSize);

        // ⚠️ The cache key carries the page, or page 2 would serve page 1's entry. Cheap to get wrong and
        // invisible in a single-page test.
        var key = $"customers-p{pageRequest.Page}-s{pageRequest.PageSize}";
        var customerCollection = await cache.GetOrCreateAsync(key, async token =>
        {
            var result = await mediator.Send(new GetCustomersQuery { Page = pageRequest }, token);
            return result.IsSuccess ? result.Value : null!;
        }, cancellationToken: cancellationToken);

        // 204 is RETIRED (ADR-023): a client always gets a parseable body. CacheAside returns default! on a
        // 500 ms lock timeout, so this branch is a cache miss rather than an empty collection.
        return customerCollection is not null
            ? TypedResults.Ok(DataResponse<PagedResponse<CustomerEntity>>.Ok(customerCollection))
            : TypedResults.Ok(DataResponse<PagedResponse<CustomerEntity>>.Ok(PagedResponse<CustomerEntity>.From([], 0, pageRequest)));
    })
    // PII-bearing read, so no longer anonymous. Breaking change with zero reachable consumers (01-api-surface.md:158).
    .WithName("GetAllCustomers")
    .RequireAuthorization();

customers.MapGet("/{email}", async Task<Results<Ok<DataResponse<CustomerEntity>>, NotFound>> (
    IMediator mediator,
    string email,
    IDistributedCache cache,
    CancellationToken cancellationToken) =>
{
    var key = $"customers-{email}";

    var customer = await cache.GetOrCreateAsync(key, async token =>
    {
        var result = await mediator.Send(new GetCustomerByEmailQuery { Email = email }, token);
        return result.IsSuccess ? result.Value : null!;
    }, cancellationToken: cancellationToken);

    if (customer is not null)
        return TypedResults.Ok(DataResponse<CustomerEntity>.Ok(customer));

    return TypedResults.NotFound();
})
    // PII-bearing read, so no longer anonymous. Breaking change with zero reachable consumers (01-api-surface.md:158).
    .WithName("GetCustomerByEmail")
    .RequireAuthorization();

// ── provider subscriptions ────────────────────────────────────────────────────────────────────
//
// A customer can only manage their own subscriptions -- OwnershipGuard.AssertOwner on {email} the
// same way UpdateCustomer already does. Subscribe/unsubscribe are idempotent by construction
// ($addToSet/$pull in CustomerService), so a repeat call is a success, not a conflict.

customers.MapPost("/{email}/subscriptions/{providerEmail}",
    async Task<Results<ForbidHttpResult, NotFound, Accepted<DataResponse<CustomerEntity>>>> (
        string email,
        string providerEmail,
        ClaimsPrincipal user,
        IMediator mediator,
        CancellationToken cancellationToken) =>
    {
        OwnershipGuard.AssertOwner(user, email);

        var result = await mediator.Send(
            new SubscribeToProviderCommand { CustomerEmail = email, ProviderEmail = providerEmail }, cancellationToken);

        if (result.IsSuccess)
            return TypedResults.Accepted("api/v1/customers", DataResponse<CustomerEntity>.Ok(result.Value));

        return TypedResults.NotFound();
    })
    .WithName("SubscribeToProvider")
    .RequireAuthorization();

customers.MapDelete("/{email}/subscriptions/{providerEmail}",
    async Task<Results<ForbidHttpResult, NotFound, Accepted<DataResponse<CustomerEntity>>>> (
        string email,
        string providerEmail,
        ClaimsPrincipal user,
        IMediator mediator,
        CancellationToken cancellationToken) =>
    {
        OwnershipGuard.AssertOwner(user, email);

        var result = await mediator.Send(
            new UnsubscribeFromProviderCommand { CustomerEmail = email, ProviderEmail = providerEmail }, cancellationToken);

        if (result.IsSuccess)
            return TypedResults.Accepted("api/v1/customers", DataResponse<CustomerEntity>.Ok(result.Value));

        return TypedResults.NotFound();
    })
    .WithName("UnsubscribeFromProvider")
    .RequireAuthorization();

customers.MapGet("/{email}/subscriptions",
    async Task<Results<ForbidHttpResult, NotFound, Ok<DataResponse<List<string>>>>> (
        string email,
        ClaimsPrincipal user,
        IMediator mediator,
        CancellationToken cancellationToken) =>
    {
        OwnershipGuard.AssertOwner(user, email);

        var result = await mediator.Send(new GetSubscribedProvidersQuery { CustomerEmail = email }, cancellationToken);

        if (result.IsSuccess)
            return TypedResults.Ok(DataResponse<List<string>>.Ok(result.Value));

        return TypedResults.NotFound();
    })
    .WithName("GetSubscribedProviders")
    .RequireAuthorization();

// ── messages and notifications ────────────────────────────────────────────────────────────────
//
// TWO NEW TOP-LEVEL ROUTE GROUPS in this process, not children of /api/v1/customers — and that is the point
// (ADR D-2). A message is addressed to a PERSON: a provider has an inbox for exactly the same reason a
// customer does, so a URL saying `customers` about a provider's inbox would assert something false and every
// client would have to work around it. Identity already hosts two unrelated groups (`/api/v1/auth` and
// `/device-token`), so this is a precedent rather than a novelty. The Customer service hosts them because it
// already owns the provider↔customer relationship these messages travel along.
//
// None of these five routes are wrapped in DataResponse<T>. They call IMessageService/INotificationService
// directly, matching AgendaBuddy.Provider.Domain.Responses.DataResponse's own GetProviderReport precedent
// (a route deliberately left outside its service's envelope for the same reason).

var messages = app.MapGroup("/api/v1/messages")
    .WithTags("MessageAPI")
    .WithOpenApi()
    .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

// The recipient is the caller's `sub` claim and there is NO parameter. A recipient parameter
// would be a thing to tamper with — `MessageService.GetInboxAsync` takes one, and passing a client-supplied
// value through would hand any authenticated caller anyone else's inbox.
messages.MapGet("/", async Task<Results<Ok<IEnumerable<MessageEntity>>, ForbidHttpResult>> (
        ClaimsPrincipal user, IMessageService service) =>
    {
        var caller = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (caller is null) return TypedResults.Forbid();

        return TypedResults.Ok(await service.GetInboxAsync(caller));
    })
    .WithName("GetInbox")
    .RequireAuthorization();

// ONE counterpart in the URL. `MessageService` derives thread_id by sorting both addresses, so
// with the caller always supplying one side, a thread between two other people has no representation in this
// URL space at all — it is unrequestable rather than merely refused.
messages.MapGet("/thread/{counterpartEmail}",
        async Task<Results<Ok<IEnumerable<MessageEntity>>, ForbidHttpResult>> (
            string counterpartEmail, ClaimsPrincipal user, IMessageService service) =>
        {
            var caller = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (caller is null) return TypedResults.Forbid();

            return TypedResults.Ok(await service.GetThreadAsync(caller, counterpartEmail));
        })
    .WithName("GetMessageThread")
    .RequireAuthorization();

messages.MapPost("/", async Task<Results<Created<MessageEntity>, ForbidHttpResult, BadRequest<string>>> (
        MessageRequest request, ClaimsPrincipal user, IMessageService service) =>
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RecipientEmail)
                            || string.IsNullOrWhiteSpace(request.Body))
            return TypedResults.BadRequest("recipientEmail and body are required.");

        var caller = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (caller is null) return TypedResults.Forbid();

        // The sender is the caller. MessageRequest has no sender field, which is the cheapest guarantee that
        // no future refactor trusts one from the body.
        var message = new MessageEntity
        {
            SenderEmail = caller,
            RecipientEmail = request.RecipientEmail,
            Body = request.Body
        };

        await service.SendMessageAsync(message);
        return TypedResults.Created($"/api/v1/messages/{message.Id}", message);
    })
    .WithName("SendMessage")
    .RequireAuthorization();

// Only the RECIPIENT may mark a message read. A sender marking their own message read is meaningless, and
// permitting it would let a sender probe whether an id exists.
messages.MapPost("/{id}/read", async Task<Results<NoContent, ForbidHttpResult>> (
        string id, ClaimsPrincipal user, IMessageService service, IRepository<MessageEntity> repository) =>
    {
        var caller = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var message = await repository.FindOneAsync(new BsonDocument("_id", new ObjectId(id)));

        // A missing message and someone else's answer identically — the same rule the notes routes follow, so
        // this cannot be used to enumerate message ids.
        try { OwnershipGuard.AssertOwner(user, message?.RecipientEmail); }
        catch (ForbiddenException) { return TypedResults.Forbid(); }

        await service.MarkReadAsync(id);
        return TypedResults.NoContent();
    })
    .WithName("MarkMessageRead")
    .RequireAuthorization();

var notifications = app.MapGroup("/api/v1/notifications")
    .WithTags("NotificationAPI")
    .WithOpenApi()
    .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

// ⚠️ THERE IS DELIBERATELY NO ROUTE THAT CREATES A NOTIFICATION. Notifications are produced by
// domain events, not by users: a create route would let any authenticated caller write a convincing "Your
// appointment was cancelled" into somebody else's list. `NotificationService.SendAsync` stays reachable
// in-process to whatever writes one.
//
// The consequence, stated so an empty list is not read as a bug: NOTHING WRITES A NOTIFICATION YET. No domain
// event calls SendAsync, so this route returns [] until something does.
notifications.MapGet("/", async Task<Results<Ok<IEnumerable<NotificationEntity>>, ForbidHttpResult>> (
        ClaimsPrincipal user, INotificationService service) =>
    {
        var caller = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (caller is null) return TypedResults.Forbid();

        return TypedResults.Ok(await service.GetForRecipientAsync(caller));
    })
    .WithName("GetNotifications")
    .RequireAuthorization();

notifications.MapPost("/{id}/read", async Task<Results<NoContent, ForbidHttpResult>> (
        string id, ClaimsPrincipal user, INotificationService service, IRepository<NotificationEntity> repository) =>
    {
        var notification = await repository.FindOneAsync(new BsonDocument("_id", new ObjectId(id)));

        try { OwnershipGuard.AssertOwner(user, notification?.RecipientEmail); }
        catch (ForbiddenException) { return TypedResults.Forbid(); }

        await service.MarkReadAsync(id);
        return TypedResults.NoContent();
    })
    .WithName("MarkNotificationRead")
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
