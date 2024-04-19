using System.Reflection;
using Kafka;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Provider.Extensions;
using Provider.Infrastructure.Data;
using Provider.Models;
using Provider.Requests;

var builder = WebApplication.CreateBuilder(args);

// Call configuration to inject dependencies
builder.Services.AddDbContexts(builder.Configuration);
builder.Services.AddHealthChecks(builder.Configuration);
builder.Services.AddMediatR(Assembly.GetExecutingAssembly());
builder.Services.AddSingleton<IKafkaClient, KafkaClient>();
builder.Services.AddSingleton<IRequestCollection, RequestCollection>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();

//Fresh Start Database
using (IServiceScope scope = app.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;
    var context = serviceProvider.GetRequiredService<ProviderContext>();
    context.Database.EnsureDeleted();
    context.Database.Migrate();
    DataSeeder.Seed(context);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

//app.RegisterEndpoints();

app.MapPost("api/v1/providers", 
    async(
        IMediator mediator, 
        ProviderContext context, 
        ProviderModel provider, 
        IRequestCollection requestCollection) =>
{
    await context.Providers!.AddAsync(provider);
    await context.SaveChangesAsync();
    await requestCollection.CreateTopicNotification(mediator, "WinniePoe");
            
    return Results.Created($"api/v1/providers/{provider.Id}", provider);
});

app.Run();
