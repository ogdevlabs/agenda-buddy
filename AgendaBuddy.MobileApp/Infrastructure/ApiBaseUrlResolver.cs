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
///   3. The hardcoded fallback, which addresses nothing under the AppHost (fixed ports were
///      removed) but keeps the app launchable outside this script.
/// </summary>
public static class ApiBaseUrlResolver
{
    public const string DefaultBaseUrl = "http://localhost:6036/";

    public static string Resolve(IConfiguration configuration, Func<string, string?> getEnvironmentVariable) =>
        getEnvironmentVariable("MAUI_API_BASE_URL")
            ?? configuration["ApiBaseUrl"]
            ?? DefaultBaseUrl;
}
