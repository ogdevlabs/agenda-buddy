using AgendaBuddy.Library.Entities;

namespace AgendaBuddy.MobileApp.Services;

/// <summary>
/// Provider's report and deactivation routes (api-contracts.md §2 — "Provider report",
/// "Provider deactivation").
/// </summary>
public interface IProviderApiService
{
    Task<ProviderReport?> GetReportAsync(CancellationToken ct = default);
    Task<bool> DeactivateAsync(CancellationToken ct = default);
}
