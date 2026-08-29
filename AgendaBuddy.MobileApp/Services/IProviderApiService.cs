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

    Task<bool> UpdateProfileAsync(string email, string firstName, string lastName, CancellationToken ct = default);
}
