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
}
