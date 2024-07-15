using System.Diagnostics;
using MiniValidation;

ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddKakfaServices(builder.Configuration);

// Enable & configure JSON Problem Details error responses
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context => CustomizeProblemDetails(context.ProblemDetails, context.HttpContext));


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var message = app.MapGroup("/api/v1/messages")
    .WithTags("MessagesAPI")
    .WithOpenApi();


message.MapPost("/create-provider-topic", async (IAdminClient adminClient, ProviderCreatedEvent @event) =>
{
    if (!MiniValidator.TryValidate(@event, out var errors))
        return TypedResults.ValidationProblem(errors);
    
    var topicName = KafkaHelper.CreateProviderTopicName(@event.Email);
    await adminClient.CreateTopicsAsync(new[]
        { new TopicSpecification { Name = topicName, NumPartitions = 1, ReplicationFactor = 1 } });
    return Results.Ok();
});

message.MapPost("/create-customer-topic", async (IAdminClient adminClient, CustomerCreatedEvent @event) =>
{
    if (!MiniValidator.TryValidate(@event, out var errors))
        return TypedResults.ValidationProblem(errors);
    
    var topicName = KafkaHelper.CreateCustomerTopicName(@event.Email);
    await adminClient.CreateTopicsAsync(new[]
        { new TopicSpecification { Name = topicName, NumPartitions = 1, ReplicationFactor = 1 } });
    return Results.Ok();
});

app.Run();

// Functions and Methods
void CustomizeProblemDetails(ProblemDetails problemDetails, HttpContext httpContext)
{
    problemDetails.Extensions["requestId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
}