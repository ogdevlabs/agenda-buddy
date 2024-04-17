using System.Reflection;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Provider.Extensions;
using Provider.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Call configuration to inject dependencies
builder.Services.AddDbContexts(builder.Configuration);
builder.Services.AddHealthChecks(builder.Configuration);
builder.Services.AddMediatR(Assembly.GetExecutingAssembly());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();

//Migrate Database
using (IServiceScope scope = app.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;
    var context = serviceProvider.GetRequiredService<ProviderContext>();
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

app.RegisterEndpoints();

app.Run();
