namespace AgendaBuddy.Library;

/// <summary>
/// The one stable local address of <c>AgendaBuddy.Gateway</c> — the single entry point every client
/// uses, so a client can hardcode it once and never rediscover a port.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the Gateway is pinned when the seven services deliberately are not.</b> AC-1.4 exists so no
/// domain service binds a fixed host port under the AppHost, and
/// <c>AppHostWiringTest.NoServiceBindsAHardcodedHostPort</c> enforces that over the seven services. The
/// Gateway is not one of them: it is the one deliberate, stable, client-facing entry point (F-015), and
/// it already removes the dynamic-port problem for everything behind it by resolving each destination
/// through Aspire service discovery at runtime. Pinning only this port leaves AC-1.4 intact for the
/// services it was written about while giving clients something durable to address.
/// </para>
/// <para>
/// <b>Outside the 6030–6039 band on purpose.</b> Those are the per-service ports a
/// <c>Local (standalone)</c> run uses (Provider 6030 … Identity 6036), and the band the AC-1.4 guard
/// test polices. Sitting outside it means a pinned Gateway can never be mistaken for — or collide with —
/// a standalone service.
/// </para>
/// <para>
/// <b>Local only.</b> In the Cloud shape the platform assigns ingress
/// (<c>WithExternalHttpEndpoints</c>), so nothing here applies; see <c>AppHostWiring.cs</c>.
/// </para>
/// <para>
/// Shared rather than duplicated so the AppHost's pinned endpoint and
/// <c>AgendaBuddy.MobileApp.Infrastructure.ApiBaseUrlResolver</c>'s fallback cannot drift apart — the
/// drift is exactly what left the mobile client addressing a dead port for the whole of F-015's life.
/// </para>
/// </remarks>
public static class LocalGatewayAddress
{
    /// <summary>The Gateway's pinned host port for a local AppHost run.</summary>
    public const int Port = 6080;

    /// <summary>The Gateway's pinned base address for a local AppHost run, trailing slash included.</summary>
    /// <remarks><c>static readonly</c> rather than <c>const</c> so it is derived from
    /// <see cref="Port"/> and the two cannot disagree.</remarks>
    public static readonly string BaseUrl = $"http://localhost:{Port}/";
}
