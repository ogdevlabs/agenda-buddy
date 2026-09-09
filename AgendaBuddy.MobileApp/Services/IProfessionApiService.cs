using AgendaBuddy.MobileApp.Models;

namespace AgendaBuddy.MobileApp.Services;

/// <summary>Catalog browse plus a provider's own selection from it — see
/// <see cref="Routing.ProfessionRouteBuilder"/>'s remarks.</summary>
public interface IProfessionApiService
{
    Task<List<ProfessionItem>> GetProfessionsAsync(CancellationToken ct = default);

    Task<List<string>> GetProviderProfessionsAsync(string email, CancellationToken ct = default);

    Task<bool> AddProfessionsToProviderAsync(string email, List<string> professionNames, CancellationToken ct = default);

    /// <summary>On failure, the visible message is localized and the server detail is diagnostic only.</summary>
    Task<ProfessionRemovalResult> RemoveProfessionFromProviderAsync(string email, string professionName, CancellationToken ct = default);
}

public sealed record ProfessionRemovalResult(bool Success, MobileError? Error)
{
    public string? ErrorMessage => Error?.Message;
}
