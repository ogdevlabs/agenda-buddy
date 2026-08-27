using AgendaBuddy.Library.Entities;

namespace MobileApp.Services;

/// <summary>
/// F-014's report and deactivation routes, never called by the client before F-015-T07
/// (api-contracts.md §2 — "Provider report", "Provider deactivation").
/// </summary>
public interface IProviderApiService
{
    Task<ProviderReport?> GetReportAsync(CancellationToken ct = default);
    Task<bool> DeactivateAsync(CancellationToken ct = default);
}
