using System.Reflection;
using Kafka;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Provider.Configurations;
using Provider.Extensions;
using Provider.Infrastructure.Data;
using Provider.Middleware;
using Provider.Models;
using Provider.Requests;

var builder = WebApplication.CreateBuilder(args);

// Call configuration to inject dependencies
builder.Services.AddDbContexts(builder.Configuration);
builder.Services.AddHealthChecks(builder.Configuration);
builder.Services.AddMediatR(Assembly.GetExecutingAssembly());
builder.Services.AddSingleton<IMongoDbConfiguration, MongoDbConfiguration>();
builder.Services.AddSingleton<IKafkaClient, KafkaClient>();
builder.Services.AddSingleton<IRequestCollection, RequestCollection>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();

//Fresh Start Database 
using (IServiceScope scope = app.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;
    // PostgreSQL
    var context = serviceProvider.GetRequiredService<ProviderContext>();
    context.Database.EnsureDeleted();
    context.Database.Migrate();
    DataSeeder.Seed(context);
    DataSeeder.SeedDocument(
        builder.Configuration,
        builder.Configuration.GetSection("MongoDB")["DatabaseName"]!,
        builder.Configuration.GetSection("MongoDB")["CollectionName"]!);
}



// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDbExceptionHandler();
app.UseHttpsRedirection();

//app.RegisterEndpoints();

app.MapPost("api/v1/providers", 
    async(
        IMediator mediator, 
        ProviderContext context, 
        ProviderModel provider, 
        IRequestCollection requestCollection) =>
{
    var iLength = provider.Email.IndexOf('@');
    var providerTopicName = provider.Email.Substring(0, iLength).ToLower()+"-topic";
    provider.Topic = providerTopicName;
    await context.Providers!.AddAsync(provider);
    await context.SaveChangesAsync();
   
    await requestCollection.CreateTopicNotification(mediator, providerTopicName);
            
    return Results.Created($"api/v1/providers/{provider.Id}", provider);
});

app.Run();
