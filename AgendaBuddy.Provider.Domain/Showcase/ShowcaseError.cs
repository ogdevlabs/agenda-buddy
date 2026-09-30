namespace AgendaBuddy.Provider.Domain.Showcase;

/// <summary>
/// A showcase failure the API maps to a status. The <see cref="Kind"/> is the contract; the message is copy.
/// </summary>
public sealed class ShowcaseError : Error
{
    public ShowcaseError(ShowcaseErrorKind kind, string field, string code, string message)
        : base(message)
    {
        Kind = kind;
        Field = field;
        Code = code;
    }

    public ShowcaseErrorKind Kind { get; }

    /// <summary>The <c>errors</c> map key a 400 reports under, e.g. <c>image</c>, <c>hash</c>, <c>caption</c>.</summary>
    public string Field { get; }

    /// <summary>A stable machine-readable code, e.g. <c>unknown-media</c> or <c>portfolio-full</c>.</summary>
    public string Code { get; }

    /// <summary>Set on a rate-limit refusal; the API reports it as <c>Retry-After</c>.</summary>
    public TimeSpan? RetryAfter { get; init; }

    public static ShowcaseError NotFound() =>
        new(ShowcaseErrorKind.NotFound, "showcase", "showcase-not-found", "This provider isn't available.");

    public static ShowcaseError Invalid(string field, string code, string message) =>
        new(ShowcaseErrorKind.Invalid, field, code, message);

    public static ShowcaseError Conflict(string code, string message) =>
        new(ShowcaseErrorKind.Conflict, "portfolio", code, message);

    public static ShowcaseError UnknownMedia() =>
        Invalid("hash", "unknown-media", "That image is not one of your uploads.");

    public static ShowcaseError RateLimited(TimeSpan retryAfter) =>
        new(ShowcaseErrorKind.RateLimited, "image", "rate-limited", "Too many uploads. Try again later.")
        {
            RetryAfter = retryAfter
        };

    public static ShowcaseError StorageUnavailable() =>
        new(ShowcaseErrorKind.StorageUnavailable, "image", "storage-unavailable",
            "Photos can't be saved right now. Try again in a few minutes.");

    public static ShowcaseError InvalidService() =>
        Invalid("serviceId", "invalid-service", "That is not one of your services.");
}

public enum ShowcaseErrorKind
{
    Invalid,
    NotFound,
    Conflict,
    RateLimited,
    StorageUnavailable
}
