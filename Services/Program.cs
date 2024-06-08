#pragma warning disable CS8321 // Local function is declared but never used

namespace Services;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
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

        var services = app.MapGroup("api/v1/services")
            .WithTags("ServiceAPI")
            .WithOpenApi()
            .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

        services.MapGet("/{email}",
            async Task<Results<Ok<IEnumerable<ServiceEntity>>, NotFound>> (
                IMediator mediator,
                string email,
                ProviderService providerService,
                IRequestCollection requestCollection) =>
            {
                var serviceEntities =
                    await EventHelper.GetServicesFromProviderEvent(requestCollection, mediator, providerService, email);
                if (serviceEntities != null)
                {
                    return TypedResults.Ok(serviceEntities);
                }

                return TypedResults.NotFound();
            }).WithName("GetServicesFromProvider");

        services.MapPut("/{email}",
            async Task<Results<ValidationProblem, NotFound, Ok<ProviderEntity>>> (IMediator mediator,
                ProviderService providerService, IRequestCollection requestCollection,
                [FromBody]List<ServiceEntity> serviceEntities, string email) =>
            {
                if (!MiniValidator.TryValidate(serviceEntities, out var errors))
                    return TypedResults.ValidationProblem(errors);
                
                var providerEntity = 
                    await EventHelper.AddServicesToProviderEvent(requestCollection, mediator,
                    providerService, serviceEntities, email);

                if (providerEntity != null)
                {
                    return TypedResults.Ok(providerEntity);
                }

                return TypedResults.NotFound();
            }).WithName("AddServicesToProvider");
        
        services.MapPatch("/{email}",
            async Task<Results<ValidationProblem, NotFound, Ok<ProviderEntity>>> (IMediator mediator,
                ProviderService providerService, IRequestCollection requestCollection,
                [FromBody]List<ServiceEntity> serviceEntities, string email) =>
            {
                if (!MiniValidator.TryValidate(serviceEntities, out var errors))
                    return TypedResults.ValidationProblem(errors);
                
                var providerEntity = 
                    await EventHelper.UpdateServicesFromProviderEvent(requestCollection, mediator,
                        providerService, serviceEntities, email);

                if (providerEntity != null)
                {
                    return TypedResults.Ok(providerEntity);
                }

                return TypedResults.NotFound();
            }).WithName("UpdateServicesFromProvider");

        app.Run();

        // Functions and Methods
        void CustomizeProblemDetails(ProblemDetails problemDetails, HttpContext httpContext) =>
            problemDetails.Extensions["requestId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        Dictionary<string, string[]> GenerateErrorMessage(string key, string[] values)
        {
            var dictionary = new Dictionary<string, string[]> { { key, values } };
            return dictionary;
        }
    }
}