using AgendaBuddy.Library.Media;
using AgendaBuddy.Provider.Api.Showcase;
using AgendaBuddy.Provider.Domain.Showcase;

namespace AgendaBuddy.Provider.Api.Modules;

/// <summary>
/// Upload and fetch. Every image is served through this authenticated route; there is no public blob URL and no
/// SAS token (ADR-069). The fetch calls the media service directly rather than through a handler, so a scrolling
/// grid does not write one audit row per thumbnail.
/// </summary>
public class MediaModule : ICarterModule
{
    /// <summary>5 MB for the image plus 1 KB of slack for the request itself.</summary>
    public const long MaxUploadBytes = 5 * 1024 * 1024 + 1024;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var media = app.MapGroup("/api/v1/media")
            .WithTags("MediaAPI")
            .WithOpenApi()
            .AddEndpointFilter<ProblemDetailsServiceEndpointFilter>()
            .RequireAuthorization();

        media.MapPost("", async (IMediator mediator, ClaimsPrincipal user, HttpContext http, CancellationToken ct) =>
            {
                var email = ShowcaseHttp.ProviderEmail(user);

                var content = await ReadBoundedAsync(http, ct);
                if (content is null)
                    return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);

                var result = await mediator.Send(new UploadMediaCommand { Email = email, Content = content }, ct);
                if (result.IsFailed)
                    return ShowcaseHttp.Failure(http, result.Errors);

                var envelope = DataResponse<MediaUploadView>.Ok(result.Value);
                return result.Value.Deduplicated
                    ? TypedResults.Ok(envelope)
                    : TypedResults.Created((string?)null, envelope);
            })
            .WithName("UploadMedia")
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadBytes))
            .Accepts<byte[]>("image/jpeg", "image/png", "image/webp")
            .Produces<DataResponse<MediaUploadView>>(StatusCodes.Status201Created)
            .Produces<DataResponse<MediaUploadView>>();

        media.MapGet("/{providerRef}/{hash}/{variant}", async (IMediaService mediaService, ClaimsPrincipal user,
                HttpContext http, string providerRef, string hash, string variant, CancellationToken ct) =>
            {
                Stream? stream;
                try
                {
                    stream = await mediaService.OpenAsync(providerRef, hash, variant, ShowcaseHttp.CallerEmail(user), ct);
                }
                catch (MediaStorageUnavailableException)
                {
                    return ShowcaseHttp.StorageUnavailable();
                }

                if (stream is null)
                    return TypedResults.NotFound();

                var etag = $"\"{hash}-{variant}\"";
                var headers = http.Response.Headers;
                headers.CacheControl = "private, max-age=31536000, immutable";
                headers.ETag = etag;
                headers.XContentTypeOptions = "nosniff";
                headers.ContentDisposition = "inline";

                if (http.Request.Headers.IfNoneMatch.Any(v => v is not null && v.Split(',').Any(t => t.Trim() == etag)))
                {
                    await stream.DisposeAsync();
                    return TypedResults.StatusCode(StatusCodes.Status304NotModified);
                }

                return TypedResults.Stream(stream, MediaService.ContentType);
            })
            .WithName("GetMedia")
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg");
    }

    /// <summary>
    /// Reads at most <see cref="MaxUploadBytes"/>; <c>null</c> when the body is larger. Checked here as well as by
    /// the request-size metadata, because a host that ignores that metadata would otherwise buffer anything.
    /// </summary>
    private static async Task<byte[]?> ReadBoundedAsync(HttpContext http, CancellationToken ct)
    {
        if (http.Request.ContentLength > MaxUploadBytes)
            return null;

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await http.Request.Body.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxUploadBytes)
                return null;
            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
