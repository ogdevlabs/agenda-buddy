using AgendaBuddy.MobileApp.Resources.Strings;

namespace AgendaBuddy.MobileApp.Models;

/// <summary>
/// The provider's own showcase: <c>GET /api/v1/showcase/me</c>, and the body every authoring <c>PUT</c> answers with.
/// </summary>
public sealed class MyShowcase
{
    public string ProviderRef { get; set; } = string.Empty;

    /// <summary>Null until the code is first requested from the Share screen.</summary>
    public string? PublicCode { get; set; }

    public string? Tagline { get; set; }
    public string? About { get; set; }
    public string? PhotoHash { get; set; }
    public string? LogoHash { get; set; }
    public List<PortfolioItem> Portfolio { get; set; } = new();
    public ShowcaseCompleteness Completeness { get; set; } = new();
    public ShowcaseFunnel Funnel { get; set; } = new();
}

public sealed class PortfolioItem
{
    public string Hash { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public string? ServiceId { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>Present on the provider's own view only.</summary>
    public DateTime? AddedAt { get; set; }

    /// <summary>Present on a customer's view only: added since that customer last looked.</summary>
    public bool IsNew { get; set; }
}

/// <summary>
/// Which of the five showcase items are done. <see cref="Missing"/> holds the server's item names:
/// <c>photo</c>, <c>logo</c>, <c>tagline</c>, <c>about</c>, <c>portfolio</c>.
/// </summary>
public sealed class ShowcaseCompleteness
{
    public int Done { get; set; }
    public int Total { get; set; } = 5;
    public List<string> Missing { get; set; } = new();

    public bool IsMissing(string item) => Missing.Any(m => string.Equals(m, item, StringComparison.OrdinalIgnoreCase));
}

public sealed class ShowcaseFunnel
{
    public int Scans { get; set; }
    public int Opened { get; set; }
    public int Booked { get; set; }
    public int WindowDays { get; set; } = 7;
}

/// <summary>
/// A showcase as a visitor sees it: <c>GET /api/v1/showcase/{providerRef}</c> and <c>/by-code/{code}</c>. It never
/// carries the provider's email, phone, appointments or customers.
/// </summary>
public sealed class ShowcaseView
{
    public string ProviderRef { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public List<string> Professions { get; set; } = new();
    public string? Tagline { get; set; }
    public string? About { get; set; }
    public string? PhotoHash { get; set; }
    public string? LogoHash { get; set; }
    public string? AvatarId { get; set; }
    public List<PortfolioItem> Portfolio { get; set; } = new();
    public List<ShowcaseServiceItem> Services { get; set; } = new();
    public ShowcaseRelationship Relationship { get; set; } = new();

    public string FullName =>
        string.Join(' ', new[] { FirstName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

public sealed class ShowcaseServiceItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Fee { get; set; }
    public string FeeType { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }

    /// <summary>Fee and duration as one line, e.g. "$450.00 per session · 60 min".</summary>
    public string DetailLabel
    {
        get
        {
            var feeType = Enum.TryParse<AgendaBuddy.Library.Entities.FeeType>(FeeType, ignoreCase: true, out var parsed)
                ? parsed
                : AgendaBuddy.Library.Entities.FeeType.Fixed;
            var fee = AppResources.Format("Fee_WithType", RuntimeText.Currency(Fee), RuntimeText.FeeType(feeType));
            return DurationMinutes > 0 ? $"{fee} · {RuntimeText.Duration(DurationMinutes)}" : fee;
        }
    }
}

public sealed class ShowcaseRelationship
{
    /// <summary>The provider previewing their own showcase. Actions are disabled and no visit is recorded.</summary>
    public bool IsSelf { get; set; }

    public bool IsSubscribed { get; set; }
    public ShowcaseNextAppointment? NextAppointment { get; set; }
    public bool HasBookedBefore { get; set; }
}

public sealed class ShowcaseNextAppointment
{
    public string Identifier { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public string? ServiceName { get; set; }
}

/// <summary><c>POST /api/v1/showcase/me/code</c>.</summary>
public sealed class ShowcasePublicCode
{
    public string Code { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

/// <summary><c>POST /api/v1/media</c>.</summary>
public sealed class MediaUpload
{
    public string Hash { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public bool Deduplicated { get; set; }
}

/// <summary>One row of <c>POST /api/v1/showcase/lookup</c>. Addresses the caller has no relationship with are omitted.</summary>
public sealed class ShowcaseLookupEntry
{
    public string Email { get; set; } = string.Empty;
    public string ProviderRef { get; set; } = string.Empty;
    public string? PhotoHash { get; set; }
    public DateTime? PortfolioChangedAt { get; set; }
}

/// <summary>One row of <c>GET /api/v1/showcase/hidden</c>.</summary>
public sealed class HiddenProvider
{
    public string ProviderRef { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public string FullName =>
        string.Join(' ', new[] { FirstName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

/// <summary>The reasons <c>POST /api/v1/showcase/{providerRef}/report</c> accepts.</summary>
public enum ShowcaseReportReason
{
    Inappropriate,
    NotTheirWork,
    Spam,
    Other
}

public static class ShowcaseReportReasons
{
    public const int DetailMaxLength = 500;

    public static string WireValue(ShowcaseReportReason reason) => reason switch
    {
        ShowcaseReportReason.Inappropriate => "inappropriate",
        ShowcaseReportReason.NotTheirWork => "not_their_work",
        ShowcaseReportReason.Spam => "spam",
        _ => "other"
    };
}

/// <summary>
/// The outcome of a showcase or media call. <see cref="ErrorCode"/> is the server's own code where it sent one
/// (see <see cref="ShowcaseErrorCodes"/>), so the copy can name what went wrong rather than "something failed".
/// </summary>
public sealed record ShowcaseResult<T>(T? Value, string? ErrorCode, int StatusCode = 200, string? FailedService = null)
{
    public bool IsSuccess => ErrorCode is null;

    public static ShowcaseResult<T> Ok(T value, int statusCode = 200) => new(value, null, statusCode);

    public static ShowcaseResult<T> Fail(string code, int statusCode, string? failedService = null) =>
        new(default, code, statusCode, failedService);
}

/// <summary>
/// Every error code the showcase and media routes can report, plus the client's own for failures that never
/// reached a route. The server's codes are its wire values verbatim.
/// </summary>
public static class ShowcaseErrorCodes
{
    public const string UnsupportedFormat = "unsupported-format";
    public const string TooLarge = "too-large";
    public const string TooManyPixels = "too-many-pixels";
    public const string DimensionExceeded = "dimension-exceeded";
    public const string Undecodable = "undecodable";
    public const string UnknownMedia = "unknown-media";
    public const string StorageUnavailable = "storage-unavailable";
    public const string PortfolioFull = "portfolio-full";
    public const string PortfolioChanged = "portfolio-changed";
    public const string ShowcaseNotFound = "showcase-not-found";
    public const string RateLimited = "rate-limited";

    /// <summary>A 400 whose field is known but whose code is not one of the image codes, e.g. a caption too long.</summary>
    public const string Invalid = "invalid";

    public const string Network = "network";
    public const string Unknown = "unknown";

    /// <summary>The codes the server itself sends, verbatim.</summary>
    public static IReadOnlyList<string> Server { get; } =
    [
        UnsupportedFormat, TooLarge, TooManyPixels, DimensionExceeded, Undecodable, UnknownMedia,
        StorageUnavailable, PortfolioFull, PortfolioChanged, ShowcaseNotFound
    ];

    /// <summary>The codes a user can be shown copy for.</summary>
    public static IReadOnlyList<string> All { get; } = [.. Server, RateLimited, Invalid, Network, Unknown];
}
