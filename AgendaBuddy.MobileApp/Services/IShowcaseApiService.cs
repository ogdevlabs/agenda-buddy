using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Routing;

namespace AgendaBuddy.MobileApp.Services;

/// <summary>
/// The Provider service's showcase and media routes. Nothing here throws for an HTTP or network failure: every
/// call answers a <see cref="ShowcaseResult{T}"/> whose code names what went wrong.
/// </summary>
public interface IShowcaseApiService
{
    Task<ShowcaseResult<MyShowcase>> GetMineAsync(CancellationToken ct = default);

    /// <summary>Both fields are always written; <c>null</c> clears one.</summary>
    Task<ShowcaseResult<MyShowcase>> SetTextAsync(string? tagline, string? about, CancellationToken ct = default);

    /// <summary><c>null</c> removes the photo, and the avatar falls back to the catalogue mark.</summary>
    Task<ShowcaseResult<MyShowcase>> SetPhotoAsync(string? hash, CancellationToken ct = default);

    Task<ShowcaseResult<MyShowcase>> SetLogoAsync(string? hash, CancellationToken ct = default);

    Task<ShowcaseResult<PortfolioItem>> AddPortfolioItemAsync(
        string hash, string? caption, string? serviceId, CancellationToken ct = default);

    Task<ShowcaseResult<PortfolioItem>> UpdatePortfolioItemAsync(
        string hash, string? caption, string? serviceId, CancellationToken ct = default);

    Task<ShowcaseResult<bool>> RemovePortfolioItemAsync(string hash, CancellationToken ct = default);

    /// <summary>
    /// Must be a permutation of the current portfolio. A <c>portfolio-changed</c> answer means another device got
    /// there first; reload and reapply.
    /// </summary>
    Task<ShowcaseResult<MyShowcase>> ReorderPortfolioAsync(IReadOnlyList<string> hashes, CancellationToken ct = default);

    Task<ShowcaseResult<ShowcasePublicCode>> GetPublicCodeAsync(CancellationToken ct = default);

    Task<ShowcaseResult<ShowcaseView>> GetShowcaseAsync(
        string providerRef, ShowcaseSource source, CancellationToken ct = default);

    Task<ShowcaseResult<ShowcaseView>> GetShowcaseByCodeAsync(
        string code, ShowcaseSource source, CancellationToken ct = default);

    /// <summary>Resolves only the caller's own counterparties; any other address is silently omitted.</summary>
    Task<ShowcaseResult<List<ShowcaseLookupEntry>>> LookupAsync(
        IReadOnlyCollection<string> emails, CancellationToken ct = default);

    Task<ShowcaseResult<bool>> ReportAsync(
        string providerRef, ShowcaseReportReason reason, string? detail, string? portfolioHash,
        CancellationToken ct = default);

    Task<ShowcaseResult<bool>> HideAsync(string providerRef, CancellationToken ct = default);

    Task<ShowcaseResult<bool>> UnhideAsync(string providerRef, CancellationToken ct = default);

    Task<ShowcaseResult<List<HiddenProvider>>> GetHiddenAsync(CancellationToken ct = default);

    /// <summary>Uploads already-resized JPEG bytes. The server re-encodes them regardless.</summary>
    Task<ShowcaseResult<MediaUpload>> UploadImageAsync(byte[] jpeg, CancellationToken ct = default);

    /// <summary>A <c>showcase-not-found</c> failure means the image, or its provider, is gone for this caller.</summary>
    Task<ShowcaseResult<byte[]>> FetchImageAsync(
        string providerRef, string hash, MediaVariant variant, CancellationToken ct = default);
}
