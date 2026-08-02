using Library.Tools;
using Microsoft.Extensions.Caching.Distributed;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddAuthorization();
// Add cache
builder.Services.AddDistributedMemoryCache();
// Add MongoDB
builder.Services.AddMongoDbRepository(builder.Configuration);
// Add MediatR
builder.Services.AddMediatR(cfg => { cfg.RegisterServicesFromAssembly(typeof(Program).Assembly); });
builder.Services.AddEventStore();
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

// JWT Bearer authentication (reads JWT_PUBLIC_KEY env var — fails fast if absent)
builder.Services.AddAgendaBuddyAuthentication();

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
app.UseAuthentication();
app.UseAuthorization();
app.UseStatusCodePages();
app.UseHttpsRedirection();

var calendar = app.MapGroup("api/v1/calendar")
    .WithTags("CalendarAPI")
    .WithOpenApi()
    .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

calendar.MapGet("/availability/{email}",
    async Task<Results<Ok<List<DateTime>>, NotFound>> (
        IMediator mediator,
        string email,
        ProviderService providerService,
        CalendarService calendarService,
        IRequestCollection requestCollection, IDistributedCache cache) =>
    {
        var key = $"availability-{email}";

        var dateTimesCollection = await cache.GetOrCreateAsync(key, async token =>
            await EventHelper.CheckCalendarAvailabilityEvent(requestCollection, mediator, providerService,
                calendarService,
                email));

        if (dateTimesCollection is null)
            return TypedResults.NotFound();

        var enumerable = dateTimesCollection.ToList();

        if (enumerable.Count != 0)
            return TypedResults.Ok(dateTimesCollection);

        return TypedResults.NotFound();
    })
    .WithName("CheckCalendarAvailability")
    .RequireAuthorization();

calendar.MapGet("/appointments/{email}",
    async Task<Results<Ok<List<AppointmentEntity>>, NotFound>> (
        IMediator mediator,
        string email,
        ProviderService providerService,
        CalendarService calendarService,
        IRequestCollection requestCollection, IDistributedCache cache) =>
    {
        var key = $"appointments-{email}";

        var appointmentEntities = await cache.GetOrCreateAsync(key, async token =>
            await EventHelper.CheckCalendarAppointmentsEvent(requestCollection, mediator, providerService,
                calendarService,
                email));

        if (appointmentEntities is not null) return TypedResults.Ok(appointmentEntities);

        return TypedResults.NotFound();
    })
    .WithName("CheckCalendarAppointments")
    .RequireAuthorization();

app.Run();

// Functions and Methods
void CustomizeProblemDetails(ProblemDetails problemDetails, HttpContext httpContext)
{
    problemDetails.Extensions["requestId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
}