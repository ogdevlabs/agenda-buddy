namespace AgendaBuddy.MobileApp.Routing;

/// <summary>
/// <c>GET /api/v1/professions</c> and <c>GET /api/v1/professions/{name}</c> stay the plain, anonymous
/// catalog browse (ADR-025 — no write route on the catalog itself). The <c>/providers/{email}</c> group
/// below is a provider's own selection *from* that catalog (added 2026-08-28) — ProviderEntity now
/// carries a <c>Professions</c> field, and these three routes are its CRUD, all
/// <c>RequireAuthorization()</c> + ownership-guarded server-side.
/// </summary>
public static class ProfessionRouteBuilder
{
    public static RouteSpec Professions() => new(HttpMethod.Get, "api/v1/professions");

    public static RouteSpec ProfessionByName(string name) => new(HttpMethod.Get, $"api/v1/professions/{name}");

    public static RouteSpec GetProviderProfessions(string email) =>
        new(HttpMethod.Get, $"api/v1/professions/providers/{Uri.EscapeDataString(email)}");

    /// <summary>Body is a bare JSON array of profession names to add — server dedupes against the
    /// existing list (<c>$addToSet</c>) and rejects any name not in the catalog.</summary>
    public static RouteSpec AddProfessionsToProvider(string email) =>
        new(HttpMethod.Put, $"api/v1/professions/providers/{Uri.EscapeDataString(email)}");

    /// <summary>Matched by name in the path. A 409 means the provider has an active appointment
    /// (<c>RemoveProfessionFromProviderCommandHandler</c>'s coarse guard) — the response body's
    /// <c>errors</c> carries the reason, not a generic failure.</summary>
    public static RouteSpec RemoveProfessionFromProvider(string email, string name) =>
        new(HttpMethod.Delete, $"api/v1/professions/providers/{Uri.EscapeDataString(email)}/{Uri.EscapeDataString(name)}");
}
