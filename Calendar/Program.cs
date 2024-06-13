using System.Diagnostics;
using Calendar.Events;
using Calendar.Extensions;
using Calendar.Requests;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Calendar;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddAuthorization();
        // Add MongoDB
        builder.Services.AddMongoDbRepository(builder.Configuration);
        // Add MediatR
        builder.Services.AddMediatR(cfg => { cfg.RegisterServicesFromAssembly(typeof(Program).Assembly); });
        // Add services required to support using MVC's model binders
        builder.Services.AddMvcCore();
        // Register Singleton instances
        builder.Services.AddSingleton<IMongoDbConfiguration, MongoDbConfiguration>();
        builder.Services.AddSingleton<IRequestCollection, RequestCollection>();
        
        // Enable & configure JSON Problem Details error responses
        builder.Services.AddProblemDetails(options =>
            options.CustomizeProblemDetails =
                context => CustomizeProblemDetails(context.ProblemDetails, context.HttpContext));

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
        
        var calendar = app.MapGroup("api/v1/calendar")
            .WithTags("CalendarAPI")
            .WithOpenApi()
            .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();
        
        calendar.MapGet("/{email}",
            async Task<Results<Ok<IEnumerable<AppointmentEntity>>, NotFound>> (
                IMediator mediator,
                string email,
                ProviderService providerService,
                IRequestCollection requestCollection) =>
            {
                var appointmentEntities =
                    await EventHelper.CheckCalendarAvailabilityEvent(requestCollection, mediator, providerService, email);
                List<AppointmentEntity> enumerable = appointmentEntities.ToList();
                if (enumerable.Any())
                {
                    return TypedResults.Ok(appointmentEntities);
                }

                return TypedResults.NotFound();
            }).WithName("CheckCalendarAvailability");
        
        app.Run();
        
        // Functions and Methods
        void CustomizeProblemDetails(ProblemDetails problemDetails, HttpContext httpContext) =>
            problemDetails.Extensions["requestId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
    }
}