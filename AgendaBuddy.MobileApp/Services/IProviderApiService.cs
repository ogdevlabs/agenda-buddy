using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Models;

namespace AgendaBuddy.MobileApp.Services;

/// <summary>
/// Provider's report and deactivation routes (api-contracts.md §2 — "Provider report",
/// "Provider deactivation").
/// </summary>
public interface IProviderApiService
{
    Task<ProviderReport?> GetReportAsync(CancellationToken ct = default);
    Task<bool> DeactivateAsync(CancellationToken ct = default);

    /// <summary>
    /// <c>GET /api/v1/providers</c> — the browse/directory list a Customer needs to find someone to book
    /// with. Mapped into the same <see cref="CustomerSummary"/> shape <see cref="ICustomerApiService"/>'s
    /// list uses, following <see cref="ViewModels.CustomersViewModel"/>'s existing convention of one
    /// contact-card model shared by both directions.
    /// </summary>
    Task<List<CustomerSummary>> GetProvidersAsync(CancellationToken ct = default);

    Task<ProfileInfo?> GetProfileAsync(string email, CancellationToken ct = default);

    /// <summary>Creates the domain profile that <c>POST api/v1/auth/register</c> does not.</summary>
    Task<bool> CreateProfileAsync(string email, string firstName, string lastName, string? phoneNumber, CancellationToken ct = default);

    Task<bool> UpdateProfileAsync(string email, string firstName, string lastName, string? phoneNumber, CancellationToken ct = default);

    /// <summary>
    /// Records this device's timezone as the provider's working-hours zone, if it differs from what the
    /// server holds. Silent, and a no-op when already correct.
    /// </summary>
    Task<bool> SyncTimeZoneAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// The provider's working-day bounds. Falls back to <see cref="WorkHours.Default"/> when they have
    /// never set them, so the caller always has a window to show.
    /// </summary>
    /// <returns><c>null</c> only when the provider could not be read at all.</returns>
    Task<WorkHours?> GetWorkHoursAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// <c>PUT /api/v1/providers/{email}/work-hours</c>. The server rejects a window that opens at or after
    /// it closes, so a <c>false</c> return can mean invalid as well as unreachable.
    /// </summary>
    Task<bool> UpdateWorkHoursAsync(string email, WorkHours hours, CancellationToken ct = default);

    /// <summary>
    /// The provider's per-weekday hours, one entry per weekday they have actually configured.
    /// </summary>
    /// <remarks>
    /// Empty for a provider who has only ever set the single pair, which is the majority — the caller fills the
    /// gaps from <see cref="GetWorkHoursAsync"/> so the screen opens showing the hours they already had rather
    /// than seven blanks.
    /// </remarks>
    Task<List<WorkDayHoursDto>> GetWorkWeekAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// <c>PUT /api/v1/providers/{email}/work-week</c>.
    /// </summary>
    /// <returns>
    /// The server's refusal when it refuses — it names which weekday is wrong, which cannot be reconstructed
    /// here from a bool.
    /// </returns>
    Task<AppointmentActionResult> UpdateWorkWeekAsync(
        string email, IEnumerable<WorkDayHoursDto> days, CancellationToken ct = default);

    /// <summary>
    /// Sets which built-in mark this account is drawn with, through the dedicated avatar route.
    /// </summary>
    /// <remarks>
    /// A targeted write. The fetch-merge-PUT in <see cref="UpdateProfileAsync"/> replaces the whole document, and
    /// doing so also resets every nested service's id (agenda-buddy-2wf) — changing an avatar must not be able to
    /// damage a provider's service catalogue.
    /// </remarks>
    Task<bool> SetAvatarAsync(string email, string avatarId, CancellationToken ct = default);

    /// <summary>
    /// Records acceptance — or non-acceptance — of the Terms and the Privacy Policy. The server stamps the time.
    /// </summary>
    Task<bool> SetConsentAsync(string email, bool acceptedTerms, bool acceptedPrivacy, CancellationToken ct = default);

    /// <summary>
    /// Erases the profile — services and embedded appointments included — and scrubs the address from the
    /// provider's customers' records.
    /// </summary>
    /// <remarks>
    /// Not <see cref="DeactivateAsync"/>, which keeps everything and only hides the provider from the directory.
    /// The domain half only: <see cref="IAuthService.DeleteAccountAsync"/> removes the credential, and must be
    /// called second because this call authorises off it.
    /// </remarks>
    Task<bool> DeleteAccountAsync(string email, CancellationToken ct = default);
}
