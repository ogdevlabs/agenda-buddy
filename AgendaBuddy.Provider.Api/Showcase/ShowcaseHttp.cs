using AgendaBuddy.Provider.Domain.Showcase;
using FluentResults;

namespace AgendaBuddy.Provider.Api.Showcase;

/// <summary>Maps a failed showcase result to its status. The <see cref="ShowcaseError.Code"/> is the contract.</summary>
public static class ShowcaseHttp
{
    public const string NotFoundType = "showcase-not-found";

    public static string CallerEmail(ClaimsPrincipal user) =>
        OwnershipGuard.ResolveCallerEmail(user) ?? throw new ForbiddenException();

    /// <summary>The caller's own address, after checking they hold the Provider role.</summary>
    public static string ProviderEmail(ClaimsPrincipal user)
    {
        OwnershipGuard.AssertRole(user, "Provider");
        return CallerEmail(user);
    }

    public static IResult Failure(HttpContext httpContext, IEnumerable<IError> errors)
    {
        var error = errors.OfType<ShowcaseError>().FirstOrDefault();
        if (error is null)
            return TypedResults.Problem(statusCode: StatusCodes.Status500InternalServerError);

        switch (error.Kind)
        {
            case ShowcaseErrorKind.Invalid:
                return TypedResults.ValidationProblem(
                    new Dictionary<string, string[]> { [error.Field] = [error.Code] },
                    detail: error.Message,
                    extensions: new Dictionary<string, object?> { ["code"] = error.Code });
            case ShowcaseErrorKind.NotFound:
                return NotFound();
            case ShowcaseErrorKind.Conflict:
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflict",
                    detail: error.Message,
                    type: error.Code,
                    extensions: new Dictionary<string, object?> { ["code"] = error.Code });
            case ShowcaseErrorKind.RateLimited:
                httpContext.Response.Headers.RetryAfter =
                    Math.Max(1, (int)Math.Ceiling((error.RetryAfter ?? TimeSpan.FromMinutes(1)).TotalSeconds)).ToString();
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status429TooManyRequests,
                    title: "Too many requests",
                    detail: error.Message,
                    extensions: new Dictionary<string, object?> { ["code"] = error.Code });
            default:
                return StorageUnavailable();
        }
    }

    public static IResult NotFound() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not found",
            detail: "This provider isn't available.",
            type: NotFoundType);

    public static IResult StorageUnavailable() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Storage unavailable",
            detail: "Photos can't be saved right now. Try again in a few minutes.",
            type: "storage-unavailable",
            extensions: new Dictionary<string, object?> { ["code"] = "storage-unavailable" });
}
