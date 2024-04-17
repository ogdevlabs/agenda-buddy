using System.Reflection;
using MediatR;
using Provider.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Call configuration to inject dependencies
builder.Services.AddDbContexts(builder.Configuration);
builder.Services.AddHealthChecks(builder.Configuration);
builder.Services.AddMediatR(Assembly.GetExecutingAssembly());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.RegisterEndpoints();

app.Run();
