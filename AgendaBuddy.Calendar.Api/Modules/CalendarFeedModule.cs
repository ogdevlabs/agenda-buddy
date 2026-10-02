using AgendaBuddy.Library.Calendar;
using AgendaBuddy.Library.Tools;

namespace AgendaBuddy.Calendar.Api.Modules;

/// <summary>
/// The caller's subscribable iCalendar feed (ADR-073/074).
/// </summary>
/// <remarks>
/// Enable, status and revoke act on the signed-in caller's own feed, taken from the token, so there is no address in
/// the request to substitute. The <c>.ics</c> fetch is anonymous because a calendar app subscribing to a URL cannot
/// send a bearer token: the URL is the credential. It is deliberately not a handler — calendar apps poll it, and an
/// audit row per poll is a per-subscriber access log this route must not keep.
/// </remarks>
public class CalendarFeedModule : ICarterModule
{
    public const string BaseUrlKey = "CalendarFeed:BaseUrl";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var feed = app.MapGroup("api/v1/calendar/feed")
            .WithTags("CalendarAPI")
            .WithOpenApi()
            .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>();

        feed.MapGet("",
                async Task<Ok<DataResponse<CalendarFeedStatus>>> (
                    IMediator mediator, ClaimsPrincipal user, CancellationToken cancellationToken) =>
                {
                    var result = await mediator.Send(
                        new GetCalendarFeedStatusQuery { OwnerEmail = CallerEmail(user) }, cancellationToken);
                    return TypedResults.Ok(DataResponse<CalendarFeedStatus>.Ok(result.Value));
                })
            .WithName("GetCalendarFeedStatus")
            .RequireAuthorization();

        // POST both enables and resets: every call issues a new token and revokes the previous URL.
        feed.MapPost("",
                async Task<Ok<DataResponse<CalendarFeedLinkResponse>>> (
                    IMediator mediator,
                    IConfiguration configuration,
                    HttpContext http,
                    ClaimsPrincipal user,
                    CalendarFeedRequest? request,
                    CancellationToken cancellationToken) =>
                {
                    var result = await mediator.Send(
                        new EnableCalendarFeedCommand { OwnerEmail = CallerEmail(user), Language = request?.Language },
                        cancellationToken);

                    http.Response.Headers.CacheControl = "no-store";
                    var url = FeedUrl(configuration, http, result.Value.Token);
                    return TypedResults.Ok(DataResponse<CalendarFeedLinkResponse>.Ok(
                        new CalendarFeedLinkResponse(url, result.Value.CreatedAt)));
                })
            .WithName("EnableCalendarFeed")
            .RequireAuthorization();

        // 204 whether or not a feed existed: turning off something already off is the outcome asked for.
        feed.MapDelete("",
                async Task<NoContent> (IMediator mediator, ClaimsPrincipal user, CancellationToken cancellationToken) =>
                {
                    await mediator.Send(new DisableCalendarFeedCommand { OwnerEmail = CallerEmail(user) }, cancellationToken);
                    return TypedResults.NoContent();
                })
            .WithName("DisableCalendarFeed")
            .RequireAuthorization();

        // Malformed, unknown and revoked tokens all answer the same empty 404, so the route is not an oracle for
        // which tokens exist.
        feed.MapGet("/{file}",
                async Task<IResult> (ICalendarFeedService calendarFeedService, HttpContext http, string file) =>
                {
                    var headers = http.Response.Headers;
                    headers["Referrer-Policy"] = "no-referrer";
                    headers.XContentTypeOptions = "nosniff";

                    var token = file.EndsWith(".ics", StringComparison.Ordinal) ? file[..^4] : string.Empty;
                    var ics = await calendarFeedService.RenderAsync(token, DateTime.UtcNow);
                    if (ics is null)
                    {
                        headers.CacheControl = "no-store";
                        // A fixed body rather than ProblemDetails, whose per-request traceId would make otherwise
                        // identical answers differ.
                        return Results.Text("Not found", "text/plain; charset=utf-8", statusCode: StatusCodes.Status404NotFound);
                    }

                    headers.CacheControl = "private, max-age=900";
                    return Results.Text(ics, "text/calendar; charset=utf-8");
                })
            .WithName("GetCalendarFeed")
            .AllowAnonymous()
            .Produces(StatusCodes.Status200OK, contentType: "text/calendar")
            .Produces(StatusCodes.Status404NotFound, contentType: "text/plain");
    }

    public static string FeedPath(string token) => $"/api/v1/calendar/feed/{token}.ics";

    private static string FeedUrl(IConfiguration configuration, HttpContext http, string token)
    {
        // The configured base is the Gateway — the address a calendar app outside the cluster can reach. Without it
        // the request's own origin is the best available answer.
        var baseUrl = configuration[BaseUrlKey];
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = $"{http.Request.Scheme}://{http.Request.Host}";
        return baseUrl.TrimEnd('/') + FeedPath(token);
    }

    private static string CallerEmail(ClaimsPrincipal user) =>
        OwnershipGuard.ResolveCallerEmail(user) ?? throw new ForbiddenException();
}
