using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Yarp.ReverseProxy.Forwarder;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// Shared plumbing for F-015-T04's AC3/AC4 (JWT passthrough) and T-303 (transport-security parity)
/// tests: both need the gateway routing a real request to a real backend service, and both need that
/// backend reachable without a real TCP socket.
/// </summary>
/// <remarks>
/// <para>
/// <b>The problem this solves.</b> <see cref="ServiceHostFixture{TEntryPoint}"/> hosts a backend service
/// over <see cref="TestServer"/> — real middleware pipeline, in-memory transport (see that file's class
/// remarks). YARP's own outbound <see cref="HttpClient"/>, by contrast, makes real network calls: it
/// needs something to dial. Standing up a second, real-socket Kestrel listener just for this task would
/// mean maintaining a hosting mode nothing else in this harness uses. Instead,
/// <see cref="BridgingForwarderHttpClientFactory"/> replaces YARP's <c>IForwarderHttpClientFactory</c> so
/// that, for the one cluster under test, "dialing the destination" means invoking the backend's own
/// <see cref="TestServer"/> handler in-process — the request still crosses YARP's real routing,
/// transform, and (F-015-T04's) failure-translation code, it just does so over the same in-memory
/// transport every other harness test already relies on.
/// </para>
/// <para>
/// <b>Why this is a request-level bridge, not a network one.</b> <see cref="TestServer.CreateHandler()"/>
/// builds an <see cref="HttpContext"/> straight from the proxied <see cref="HttpRequestMessage"/> — its
/// <c>RequestUri</c> supplies <c>Scheme</c>/<c>Host</c>/<c>Path</c>/<c>Query</c> exactly as a real socket
/// dial would, and its headers (including <c>Authorization</c> and YARP's own
/// <c>X-Forwarded-*</c>/<c>Host</c> transforms) arrive unmodified. Nothing about the destination's own
/// pipeline — auth, ownership, transport security — can tell the difference.
/// </para>
/// </remarks>
internal static class GatewayToRealServiceHarness
{
    /// <summary>
    /// Builds a <see cref="WebApplicationFactory{TEntryPoint}"/> hosting the real Gateway pipeline, with
    /// exactly one cluster's destination address resolvable (the others are simply absent from the fake
    /// Aspire service-discovery configuration, so <c>AspireServiceDiscoveryProxyConfigProvider</c>
    /// registers no route for them — never a route pointing nowhere) and that one cluster's outbound
    /// calls bridged in-process to <paramref name="backend"/>.
    /// </summary>
    /// <param name="clusterId">
    /// The logical/Aspire service name (e.g. <c>"booking"</c>, <c>"profession"</c>) — matches
    /// <c>AspireServiceDiscoveryProxyConfigProvider</c>'s route table.
    /// </param>
    /// <param name="backend">The already-started backend's <see cref="TestServer"/> to bridge to.</param>
    /// <param name="destinationScheme">
    /// The scheme YARP's outbound request carries, and therefore the scheme the backend's own
    /// <c>HttpContext.Request.Scheme</c> resolves to — since neither Gateway nor any of the seven
    /// backends calls <c>UseForwardedHeaders()</c>, the inbound (client-to-gateway) scheme has no bearing
    /// on what the backend sees for itself; only the destination address's own scheme does. <c>"http"</c>
    /// unless a test needs to prove HSTS/redirect parity over what YARP would forward as an
    /// <c>https</c> destination (T-303).
    /// </param>
    public static WebApplicationFactory<GatewayAnchor> CreateFactory(
        string clusterId, TestServer backend, string destinationScheme = "http") =>
        new WebApplicationFactory<GatewayAnchor>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"services:{clusterId}:{destinationScheme}:0"] =
                        $"{destinationScheme}://{clusterId}-through-gateway",
                }));

            builder.ConfigureServices(services => services.AddSingleton<IForwarderHttpClientFactory>(
                new BridgingForwarderHttpClientFactory(clusterId, backend)));
        });

    /// <summary>
    /// Routes YARP's outbound call for one specific cluster to a backend's in-memory
    /// <see cref="TestServer"/> instead of dialing a real socket. See the class remarks above for why.
    /// </summary>
    private sealed class BridgingForwarderHttpClientFactory(string clusterId, TestServer backend)
        : IForwarderHttpClientFactory
    {
        public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context)
        {
            if (context.ClusterId != clusterId)
            {
                throw new InvalidOperationException(
                    $"{nameof(GatewayToRealServiceHarness)} is bridged for cluster '{clusterId}' only, "
                    + $"but YARP asked for a client for '{context.ClusterId}'. Add another bridge or a "
                    + "fallback factory if a test needs to call more than one real backend.");
            }

            return new HttpMessageInvoker(backend.CreateHandler());
        }
    }
}
