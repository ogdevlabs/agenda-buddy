using Library.Tools;
using Microsoft.Extensions.Caching.Distributed;

ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;


var builder = WebApplication.CreateBuilder(args);

// Aspire defaults: telemetry, health checks, service discovery, HttpClient resilience.
builder.AddServiceDefaults();

// One MongoDB client per process, shared by the repositories, IMongoDbConfiguration and the
// EventStore. Aspire injects ConnectionStrings:mongodb; the resolver also accepts every legacy
// shape, and fails with a message naming each key it tried rather than a null-argument throw.
builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(MongoConnectionResolver.Resolve(builder.Configuration)));

// Readiness probe. Singleton so the 5s result cache is process-wide.
builder.Services.AddSingleton<MongoHealthCheck>();
builder.Services.AddHealthChecks()
    .AddCheck<MongoHealthCheck>("mongodb", tags: ["ready"]);
// Add MongoDB
builder.Services.AddMongoDbRepository(builder.Configuration);
// Add Cache
builder.Services.AddDistributedMemoryCache();
// Add MediatR
builder.Services.AddMediatR(cfg => { cfg.RegisterServicesFromAssembly(typeof(Program).Assembly); });
builder.Services.AddEventStore();
// Add services required to support using MVC's model binders
builder.Services.AddMvcCore();
// Register Singleton instances
// Explicit factory: MongoDbConfiguration now has both an IMongoClient and a legacy
// IConfiguration constructor, and the container cannot choose between them on its own.
builder.Services.AddSingleton<IMongoDbConfiguration>(serviceProvider =>
    new MongoDbConfiguration(serviceProvider.GetRequiredService<IMongoClient>()));
// Scoped, not Singleton: RequestCollection consumes the scoped IEventStore, and a
// singleton capturing it fails DI validation — which is enabled in Development, the
// environment the AppHost runs services in. RequestCollection is stateless, so request
// scope is the correct lifetime rather than a workaround.
builder.Services.AddScoped<IRequestCollection, RequestCollection>();

// Enable & configure JSON Problem Details error responses
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails =
        context => CustomizeProblemDetails(context.ProblemDetails, context.HttpContext));

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

var services = app.MapGroup("api/v1/services")
    .WithTags("ServiceAPI")
    .WithOpenApi()
    .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

services.MapGet("/{email}",
    async Task<Results<Ok<List<ServiceEntity>>, NotFound>> (
        IMediator mediator,
        string email,
        ProviderService providerService,
        IRequestCollection requestCollection, IDistributedCache cache) =>
    {
        var key = $"services-{email}";

        var serviceEntities = await cache.GetOrCreateAsync(key,
            async token =>
                await EventHelper.GetServicesFromProviderEvent(requestCollection, mediator, providerService, email));

        if (serviceEntities != null)
            return TypedResults.Ok(serviceEntities);

        return TypedResults.NotFound();
    }).WithName("GetServicesFromProvider");

services.MapPut("/{email}",
    async Task<Results<ValidationProblem, ForbidHttpResult, NotFound, Ok<ProviderEntity>>> (IMediator mediator,
        ClaimsPrincipal user,
        ProviderService providerService, IRequestCollection requestCollection,
        [FromBody] List<ServiceEntity> serviceEntities, string email) =>
    {
        if (!MiniValidator.TryValidate(serviceEntities, out var errors))
            return TypedResults.ValidationProblem(errors);

        try { OwnershipGuard.AssertOwner(user, email); }
        catch (ForbiddenException) { return TypedResults.Forbid(); }

        var providerEntity =
            await EventHelper.AddServicesToProviderEvent(requestCollection, mediator,
                providerService, serviceEntities, email);

        if (providerEntity != null)
            return TypedResults.Ok(providerEntity);

        return TypedResults.NotFound();
    })
    .WithName("AddServicesToProvider")
    .RequireAuthorization();

services.MapPatch("/{email}",
    async Task<Results<ValidationProblem, ForbidHttpResult, NotFound, Ok<ProviderEntity>>> (IMediator mediator,
        ClaimsPrincipal user,
        ProviderService providerService, IRequestCollection requestCollection,
        [FromBody] List<ServiceEntity> serviceEntities, string email) =>
    {
        if (!MiniValidator.TryValidate(serviceEntities, out var errors))
            return TypedResults.ValidationProblem(errors);

        try { OwnershipGuard.AssertOwner(user, email); }
        catch (ForbiddenException) { return TypedResults.Forbid(); }

        var providerEntity =
            await EventHelper.UpdateServicesFromProviderEvent(requestCollection, mediator,
                providerService, serviceEntities, email);

        if (providerEntity != null)
            return TypedResults.Ok(providerEntity);

        return TypedResults.NotFound();
    })
    .WithName("UpdateServicesFromProvider")
    .RequireAuthorization();

app.Run();

// Functions and Methods
void CustomizeProblemDetails(ProblemDetails problemDetails, HttpContext httpContext)
{
    problemDetails.Extensions["requestId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
}

