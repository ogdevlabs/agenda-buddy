using System.Net;

ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;


var builder = WebApplication.CreateBuilder(args);

// Add MongoDB
builder.Services.AddMongoDbRepository(builder.Configuration);

// Add MediatR
builder.Services.AddMediatR(cfg => { cfg.RegisterServicesFromAssembly(typeof(Program).Assembly); });

// Add services required to support using MVC's model binders
builder.Services.AddMvcCore();

// Register Singleton instances
builder.Services.AddSingleton<IMongoDbConfiguration, MongoDbConfiguration>();
builder.Services.AddSingleton<IKafkaClient, KafkaClient>();
builder.Services.AddSingleton<IRequestCollection, RequestCollection>();

// Enable & configure JSON Problem Details error responses
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context => CustomizeProblemDetails(context.ProblemDetails, context.HttpContext));

// Add Anti-CSRF/XSRF services
builder.Services.AddAntiforgery();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();

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
            {
                exceptionContext.Response.StatusCode = badRequestEx.StatusCode;
            }

            if (exceptionContext.Request.AcceptsJson()
                && exceptionContext.RequestServices.GetRequiredService<IProblemDetailsService>() is
                    { } problemDetailsService)
            {
                // Write as JSON problem details
                await problemDetailsService.WriteAsync(new()
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

        var iLength = providerEntity.Email.IndexOf('@');
        var topicName = providerEntity.Email.Substring(0, iLength).ToLower() + "-topic";
        providerEntity.KafkaTopic = topicName;
        var filter =
            SupportTools<ProviderEntity>.FilterByNameAndLastName(providerEntity.FirstName, providerEntity.LastName);
        var existingProvider = await providerService.FindProviders(filter);

        if (existingProvider != null)
            return TypedResults.ValidationProblem(GenerateErrorMessage(
                "Existing record found", new string[]
                {
                    $"Email:{providerEntity.Email}"
                }));

        var eventResponse =
            await EventsHelper.AddProviderEvent(requestCollection, mediator, providerService, providerEntity);
        if (!string.IsNullOrEmpty(eventResponse) && !eventResponse.ToLower().StartsWith("exception"))
        {
            return TypedResults.Created($"/api/v1/providers/{providerEntity.Id}", providerEntity);
        }

        return TypedResults.ValidationProblem(GenerateErrorMessage(
            "Kafka Error", new string[] { "Kafka Topic", $"{topicName}" })
        );
    })
    .WithName("CreateProvider");

// Get provider list
providers.MapGet("", async Task<Results<Ok<IEnumerable<ProviderEntity>>, NoContent>> (IMediator mediator,
    ProviderService providerService,
    IRequestCollection requestCollection) =>
{
    var providerCollection = await EventsHelper.GetProvidersEvent(requestCollection, mediator, providerService);
    return TypedResults.Ok(providerCollection);
}).WithName("GetAllProviders");

// Get provider by Email
providers.MapGet("/{email}", async Task<Results<Ok<ProviderEntity>, NotFound>> (IMediator mediator,
    string email,
    ProviderService providerService,
    IRequestCollection requestCollection) =>
{
    var record = await EventsHelper.GetProviderByEmail(requestCollection, mediator, providerService, email);
    if (record != null)
    {
        return TypedResults.Ok(record);
    }
    else
    {
        return TypedResults.NotFound();
    }
}).WithName("GetProviderByEmail");


// Update a provider, using email for search of the record
providers.MapPut("/{email}", async Task<Results<ValidationProblem, NotFound, Accepted>> (
    string email,
    IMediator mediator,
    ProviderService providerService,
    ProviderEntity providerEntity,
    IRequestCollection requestCollection) =>
{
    if (!MiniValidator.TryValidate(providerEntity, out var errors))
        return TypedResults.ValidationProblem(errors);

    var eventResponse =
        await EventsHelper.UpdateProviderEvent(email, requestCollection, mediator, providerService, providerEntity);

    if (!string.IsNullOrEmpty(eventResponse))
    {
        return TypedResults.Accepted("api/v1/providers");
    }

    return TypedResults.NotFound();
}).WithName("UpdateProvider");

app.Run();


// Functions and Methods
void CustomizeProblemDetails(ProblemDetails problemDetails, HttpContext httpContext) =>
    problemDetails.Extensions["requestId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

Dictionary<string, string[]> GenerateErrorMessage(string key, string[] values)
{
    var dictionary = new Dictionary<string, string[]> { { key, values } };
    return dictionary;
}