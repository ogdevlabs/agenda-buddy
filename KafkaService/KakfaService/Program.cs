using System.Diagnostics;
using System.Text.Json;
using EventAndCommands.Persitency;
using Library.Events;
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
    await adminClient.CreateTopicsAsync(new[] { CreateTopicSpecification(topicName) });

    return Results.Ok();
});

message.MapPost("/create-customer-topic", async (IAdminClient adminClient, CustomerCreatedEvent @event) =>
{
    if (!MiniValidator.TryValidate(@event, out var errors))
        return TypedResults.ValidationProblem(errors);

    var topicName = KafkaHelper.CreateCustomerTopicName(@event.Email);
    await adminClient.CreateTopicsAsync(new[] { CreateTopicSpecification(topicName) });

    return Results.Ok();
});

message.MapPost("/subscribe", async (IProducer<Null, string> producer, SubscriptionEvent @event) =>
{
    if (!MiniValidator.TryValidate(@event, out var errors))
        return TypedResults.ValidationProblem(errors);
    var topicName = @event.Subscription!.TopicToSubscribe;
    var consumerEmail = @event.Subscription!.ConsumerEmail;
    var subscriberTopic = @event.Subscription.ConsumerTopic;

    // Provide subscription status to a topic
    var statusMessage = JsonSerializer.Serialize(new SubscriptionStatus(consumerEmail, "Subscribed"));
    await producer.ProduceAsync(subscriberTopic, new Message<Null, string> { Value = statusMessage });

    // Notify topic of the new subscription
    var verificationMessage = JsonSerializer.Serialize(new Verification("Consumer subscribed"));
    await producer.ProduceAsync(topicName, new Message<Null, string> { Value = verificationMessage });

    return Results.Ok();
});

app.Run();

// Functions and Methods
void CustomizeProblemDetails(ProblemDetails problemDetails, HttpContext httpContext)
{
    problemDetails.Extensions["requestId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
}

TopicSpecification CreateTopicSpecification(string topicName)
{
    return new TopicSpecification()
    {
        Name = topicName,
        NumPartitions = 1,
        ReplicationFactor = 1
    };
}