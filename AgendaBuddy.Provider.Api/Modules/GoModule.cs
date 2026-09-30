using AgendaBuddy.Library.Showcase;
using AgendaBuddy.Provider.Api.Showcase;

namespace AgendaBuddy.Provider.Api.Modules;

/// <summary>
/// The only anonymous route in the showcase. It answers identically for known, unknown and malformed codes, apart
/// from an anonymous per-platform counter, and stores no address, browser identifier, timestamp or visitor id.
/// Deliberately not a handler: an audit row per scan would be exactly the per-visitor record this route must not keep.
/// </summary>
public class GoModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/go/{code}", async (IShowcaseService showcaseService, IConfiguration configuration,
                HttpContext http, string code) =>
            {
                var headers = http.Response.Headers;
                headers.CacheControl = "no-store";
                headers["Referrer-Policy"] = "no-referrer";
                headers.XContentTypeOptions = "nosniff";
                headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'";

                var platform = StoreRedirect.Classify(http.Request.Headers.UserAgent.ToString());
                await showcaseService.IncrementGoCounterAsync(code, platform);

                var appStore = configuration[StoreRedirect.AppStoreUrlKey];
                var playStore = configuration[StoreRedirect.PlayStoreUrlKey];
                var target = platform switch
                {
                    StoreRedirect.Ios => appStore,
                    StoreRedirect.Android => playStore,
                    _ => null
                };

                return StoreRedirect.IsRedirectable(target)
                    ? Results.Redirect(target!)
                    : Results.Content(StoreRedirect.ChooserPage(appStore, playStore), "text/html; charset=utf-8");
            })
            .WithTags("GoAPI")
            .WithOpenApi()
            .WithName("GoRedirect")
            .AllowAnonymous()
            .RequireRateLimiting(ShowcaseRateLimiting.GoPolicy)
            .Produces(StatusCodes.Status302Found)
            .Produces(StatusCodes.Status200OK, contentType: "text/html");
    }
}
