ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

var builder = WebApplication.CreateBuilder(args);

// Add MongoDB
builder.Services.AddMongoDbRepository(builder.Configuration);

// Add MediatR
builder.Services.AddMediatR(cfg => { cfg.RegisterServicesFromAssembly(typeof(Program).Assembly); });

// Add services required to support using MVC's model binders
builder.Services.AddMvcCore();

// Register Singleton instances
builder.Services.AddSingleton<IMongoDbConfiguration, MongoDbConfiguration>();
builder.Services.AddSingleton<IKafkaClient, KafkaClient>();
builder.Services.AddSingleton<IRequestCollection, RequestCollection>();

// Enable & configure JSON Problem Details error responses
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context => CustomizeProblemDetails(context.ProblemDetails, context.HttpContext));

// Add Anti-CSRF/XSRF services
builder.Services.AddAntiforgery();

// JWT Bearer authentication (reads JWT_PUBLIC_KEY env var — fails fast if absent)
builder.Services.AddAgendaBuddyAuthentication();
builder.Services.AddAuthorization();

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

var booking = app.MapGroup("api/v1/booking")
    .WithTags("BookingAPI")
    .WithOpenApi()
    .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

booking.MapPost("/appointments",
        async Task<Results<ValidationProblem, Created<AppointmentEntity>, BadRequest>> (IMediator mediator,
            ProviderService providerService, BookingService bookingService, AppointmentEntity appointmentEntity,
            IRequestCollection requestCollection) =>
        {
            if (!MiniValidator.TryValidate(appointmentEntity, out var errors))
                return TypedResults.ValidationProblem(errors);

            var index = appointmentEntity.EmailProvider.IndexOf('@');
            var topicName = appointmentEntity.EmailProvider.Substring(0, index).ToLower() + "-topic";

            var eventResponse = await EventsHelper.BookAppointmentEvent(requestCollection, mediator, providerService,
                bookingService,
                appointmentEntity);

            if (!string.IsNullOrEmpty(eventResponse) && !eventResponse.ToLower().StartsWith("exception"))
                return TypedResults.Created($"/api/v1/appointments/{appointmentEntity.Identifier}", appointmentEntity);
            return TypedResults.ValidationProblem(GenerateErrorMessage(
                "No record match found error", new[] { "No provider", $"{appointmentEntity.EmailProvider}" }));
        })
    .WithName("BookAppointment");

booking.MapPut("/appointments/",
        async Task<Results<ValidationProblem, Accepted<AppointmentEntity>, BadRequest>> (IMediator mediator,
            ProviderService providerService, BookingService bookingService, AppointmentEntity appointmentEntity,
            IRequestCollection requestCollection) =>
        {
            if (!MiniValidator.TryValidate(appointmentEntity, out var errors))
                return TypedResults.ValidationProblem(errors);

            var index = appointmentEntity.EmailProvider.IndexOf('@');
            var topicName = appointmentEntity.EmailProvider.Substring(0, index).ToLower() + "-topic";

            var eventResponse = await EventsHelper.UpdateAppointmentEvent(requestCollection, mediator, providerService,
                bookingService,
                appointmentEntity);

            if (!string.IsNullOrEmpty(eventResponse) && !eventResponse.ToLower().StartsWith("exception"))
                return TypedResults.Accepted($"/api/v1/appointments/{appointmentEntity.Identifier}", appointmentEntity);
            return TypedResults.ValidationProblem(GenerateErrorMessage(
                "Update Appointment Error",
                new[] { "Error when trying to update appointment identifier:", $"{appointmentEntity.Identifier}" }));
        })
    .WithName("UpdateAppointment");

booking.MapDelete("/appointments/",
        async Task<Results<ValidationProblem, NoContent, BadRequest>> (IMediator mediator,
            ProviderService providerService, BookingService bookingService,
            [FromBody] AppointmentEntity appointmentEntity,
            IRequestCollection requestCollection) =>
        {
            if (!MiniValidator.TryValidate(appointmentEntity, out var errors))
                return TypedResults.ValidationProblem(errors);

            var index = appointmentEntity.EmailProvider.IndexOf('@');
            var topicName = appointmentEntity.EmailProvider.Substring(0, index).ToLower() + "-topic";

            var eventResponse = await EventsHelper.CancelAppointmentEvent(requestCollection, mediator, providerService,
                bookingService,
                appointmentEntity);

            if (!string.IsNullOrEmpty(eventResponse) && !eventResponse.ToLower().StartsWith("exception"))
                return TypedResults.NoContent();
            return TypedResults.ValidationProblem(GenerateErrorMessage(
                "Cancel Appointment Error",
                new[] { "Error when trying to cancel appointment identifier:", $"{appointmentEntity.Identifier}" }));
        })
    .WithName("CancelAppointment");

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