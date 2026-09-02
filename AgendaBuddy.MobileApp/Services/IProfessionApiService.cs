using AgendaBuddy.MobileApp.Models;

namespace AgendaBuddy.MobileApp.Services;

/// <summary>Catalog browse plus a provider's own selection from it — see
/// <see cref="Routing.ProfessionRouteBuilder"/>'s remarks.</summary>
public interface IProfessionApiService
{
    Task<List<ProfessionItem>> GetProfessionsAsync(CancellationToken ct = default);

    Task<List<string>> GetProviderProfessionsAsync(string email, CancellationToken ct = default);

    Task<bool> AddProfessionsToProviderAsync(string email, List<string> professionNames, CancellationToken ct = default);

    /// <summary>On failure, <see cref="ProfessionRemovalResult.ErrorMessage"/> carries the server's
    /// reason when one was given (e.g. the active-appointments guard) — null for a generic failure.</summary>
    Task<ProfessionRemovalResult> RemoveProfessionFromProviderAsync(string email, string professionName, CancellationToken ct = default);
}

public sealed record ProfessionRemovalResult(bool Success, string? ErrorMessage);
