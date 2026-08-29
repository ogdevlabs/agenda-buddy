using AgendaBuddy.Library;
using Microsoft.Extensions.Configuration;

namespace AgendaBuddy.MobileApp.Infrastructure;

/// <summary>
/// Resolves the base address for the "AgendaBuddyApi" / "AgendaBuddyApiNoAuth" named HttpClients.
/// Not guarded by #if MOBILE — Microsoft.Extensions.Configuration.Abstractions is a
/// transitive dependency of Microsoft.Extensions.Http, which AgendaBuddy.MobileApp.csproj references
/// unconditionally, so this compiles under the net10.0 fallback TFM too and is directly unit
/// testable from AgendaBuddy.MobileApp.Tests without a Maui bootstrap (same pattern as AgendaBuddy.MobileApp/Routing/*).
///
/// Priority, highest first:
///   1. The MAUI_API_BASE_URL environment variable — set by scripts/run-ios.sh from the gateway's
///      discovered, dynamically-assigned port (ARCHITECTURE.md §3, Design Round 1 Q2).
///   2. The "ApiBaseUrl" configuration key — never actually populated today, since nothing loads
///      AgendaBuddy.MobileApp/appsettings.json into IConfiguration, but kept as the documented override point.
///   3. The fallback — the Gateway's pinned local address. Previously this was a dead port: it named
///      6036, which is <i>Identity's</i> standalone port, not the Gateway's, and under the AppHost
///      nothing listened there at all, so launching the app without step 1 sent every request into a
///      void and looked like "wrong password". It now points at the one address the AppHost reserves.
/// </summary>
public static class ApiBaseUrlResolver
{
    /// <summary>
    /// Shared with the AppHost rather than restated, so the pinned endpoint and this fallback cannot
    /// drift apart — see <see cref="LocalGatewayAddress"/>'s remarks.
    /// </summary>
    public static readonly string DefaultBaseUrl = LocalGatewayAddress.BaseUrl;

    public static string Resolve(IConfiguration configuration, Func<string, string?> getEnvironmentVariable) =>
        getEnvironmentVariable("MAUI_API_BASE_URL")
            ?? configuration["ApiBaseUrl"]
            ?? DefaultBaseUrl;
}
