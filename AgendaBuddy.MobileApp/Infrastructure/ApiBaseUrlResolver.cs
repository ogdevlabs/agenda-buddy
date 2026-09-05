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

    /// <summary>
    /// The address the Android emulator reaches the HOST machine on. <c>localhost</c> inside the emulator is
    /// the emulator itself.
    /// </summary>
    internal const string AndroidEmulatorHostAlias = "10.0.2.2";

    public static string Resolve(IConfiguration configuration, Func<string, string?> getEnvironmentVariable) =>
        getEnvironmentVariable("MAUI_API_BASE_URL")
            ?? configuration["ApiBaseUrl"]
            ?? DefaultBaseUrl;

    /// <summary>
    /// Rewrites a loopback host to the alias the Android emulator reaches the host machine on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Applied on Android only, and only to a loopback address. An emulator is a separate device: pointing it
    /// at <c>localhost:6080</c> makes it dial itself, where nothing listens, and every request fails in a way
    /// that looks like a dead backend. The iOS simulator shares the host's network stack and needs no
    /// rewriting, which is why this cannot live in <see cref="Resolve"/> for every platform.
    /// </para>
    /// <para>
    /// A deployed address is returned untouched — the test for that is the point, because a rewrite that fired
    /// on a real hostname would silently redirect a production build at a private address.
    /// </para>
    /// </remarks>
    public static string RemapLoopbackForAndroidEmulator(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return baseUrl;

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)) return baseUrl;

        var isLoopback = uri.IsLoopback
            || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);

        if (!isLoopback) return baseUrl;

        return new UriBuilder(uri) { Host = AndroidEmulatorHostAlias }.Uri.ToString();
    }
}
