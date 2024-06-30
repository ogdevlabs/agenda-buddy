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

var professions = app.MapGroup("api/v1/professions")
    .WithTags("ProfessionAPI")
    .WithOpenApi()
    .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

professions.MapPost("/",
    async Task<Results<ValidationProblem, Created<ProfessionEntity>>> (IMediator mediator,
        ProfessionService professionService, ProfessionEntity professionEntity,
        IRequestCollection requestCollection) =>
    {
        if (!MiniValidator.TryValidate(professionEntity, out var errors))
            return TypedResults.ValidationProblem(errors);
        var profession = await professionService.GetProfessionAsync(professionEntity.Name);
        if (profession != null)
        {
            return TypedResults.ValidationProblem(GenerateErrorMessage(
                "Existing record found", new[]
                {
                    $"Name:{professionEntity.Name}"
                }));
        }

        var eventResponse =
            await EventsHelper.AddProfessionEvent(requestCollection, mediator, professionService, professionEntity);

        if (eventResponse != null)
            return TypedResults.Created($"api/v1/professions/{professionEntity.Id}", professionEntity);

        return TypedResults.ValidationProblem(GenerateErrorMessage(
            "Error", ["Error adding profession:", $"{professionEntity.Name}"])
        );
    }).WithName("CreateProfession");

professions.MapGet("",
    async Task<Results<Ok<IEnumerable<ProfessionEntity>>, NoContent>> (IRequestCollection requestCollection,
        IMediator mediator, ProfessionService professionService) =>
    {
        var professionCollection =
            await EventsHelper.GetAllProfessionsEvent(requestCollection, mediator, professionService);
        return TypedResults.Ok(professionCollection);
    }).WithName("GetProfesssions");

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