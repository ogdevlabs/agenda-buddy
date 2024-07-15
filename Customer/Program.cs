using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add MongoDB
builder.Services.AddMongoDbRepository(builder.Configuration);

// Add Distributed Cache
builder.Services.AddDistributedMemoryCache();

// Add MediatR
builder.Services.AddMediatR(cfg => { cfg.RegisterServicesFromAssembly(typeof(Program).Assembly); });

// Add services required to support using MVC's model binders
builder.Services.AddMvcCore();

// Register Singleton instances
builder.Services.AddSingleton<IMongoDbConfiguration, MongoDbConfiguration>();
builder.Services.AddSingleton<IRequestCollection, RequestCollection>();
builder.Services.AddSingleton<IKafkaRequestCollection, KafkaRequestCollection>();

// Kafka
builder.Services.AddKafkaBootstrap(builder.Configuration);
builder.Services.AddKakfaServices(builder.Configuration);

// Enable & configure JSON Problem Details error responses
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context => CustomizeProblemDetails(context.ProblemDetails, context.HttpContext));

// Add Anti-CSRF/XSRF services
builder.Services.AddAntiforgery();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var app = builder.Build();

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
    IRequestCollection requestCollection,
    IKafkaRequestCollection kafkaRequestCollection,
    IProducer<Null, string> producer) =>
{
    if (!MiniValidator.TryValidate(customerEntity, out var errors))
        return TypedResults.ValidationProblem(errors);
    var filter =
        SupportTools<CustomerEntity>.FilterByNameAndLastName(customerEntity.FirstName!, customerEntity.LastName!);
    var existingCustomer = await customerService.FindCustomerAsync(filter);
    
    if (existingCustomer != null)
        return TypedResults.ValidationProblem(GenerateErrorMessage(
            "Existing record found", new[]
            {
                $"Email:{customerEntity.Email}"
            }));
    
    // Create Kafka Customer Topic 
    var @event = new CustomerCreatedEvent { Email = customerEntity.Email! };
    var topicResponse =
        await KafkaEvents.CreateCustomerTopicEvent(mediator, @event, kafkaRequestCollection, customerEntity.Email!,
            false);
    if (!string.IsNullOrEmpty(topicResponse))
    {
        customerEntity.KafkaTopic = topicResponse;
        var message = JsonSerializer.Serialize(@event);
        var response = await producer.ProduceAsync(topicResponse, new Message<Null, string> { Value = message });
    }
    
    // Create customer
    var eventResponse =
        await EventsHelper.AddCustomerEvent(requestCollection, mediator, customerService, customerEntity);
    if (!string.IsNullOrEmpty(eventResponse) && !eventResponse.ToLower().StartsWith("exception"))
        return TypedResults.Created($"/api/v1/customers/{customerEntity.Id}", customerEntity);

    return TypedResults.ValidationProblem(GenerateErrorMessage(
        "Kafka Error", new[] { "Kafka Topic", $"{topicResponse}" })
    );
}).WithName("CreateCustomer");

customers.MapPut("/{email}",
    async Task<Results<ValidationProblem, NotFound, Accepted>> (string email, IMediator mediator,
        CustomerService customerService, CustomerEntity customerEntity, IRequestCollection requestCollection) =>
    {
        if (!MiniValidator.TryValidate(customerEntity, out var errors))
            return TypedResults.ValidationProblem(errors);

        var eventResponse =
            await EventsHelper.UpdateCustomerEvent(email, requestCollection, mediator, customerService, customerEntity);

        if (!string.IsNullOrEmpty(eventResponse)) return TypedResults.Accepted("api/v1/customers");

        return TypedResults.NotFound();
    }).WithName("UpdateCustomer");

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

customers.MapPost("/subscribe",
    async Task<Results<ValidationProblem, NotFound, Accepted>> (IRequestCollection requestCollection,
        IMediator mediator, [FromBody] CustomerSubscribedToProviderEntity customerSubscribedToProviderEntity,
        KafkaProducer kafkaProducer) =>
    {
        if (!MiniValidator.TryValidate(customerSubscribedToProviderEntity, out var errors))
            return TypedResults.ValidationProblem(errors);
        var message =
            await EventsHelper.SubscribeToProviderEvent(requestCollection, mediator, customerSubscribedToProviderEntity,
                kafkaProducer);

        if (!string.IsNullOrEmpty(message)) return TypedResults.Accepted("api/v1/customers");

        return TypedResults.NotFound();
    }).WithName("SubscribeToProvider");

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