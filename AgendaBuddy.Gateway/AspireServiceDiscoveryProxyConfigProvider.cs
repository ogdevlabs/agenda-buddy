using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace AgendaBuddy.Gateway;

/// <summary>
/// F-015-T02 spiked this against a single ("booking") destination; F-015-T03 expands it to the full
/// seven-service allowlist. Builds YARP's route/cluster table by reading the same Aspire
/// service-discovery configuration keys (<c>services:&lt;name&gt;:http:0</c> /
/// <c>services:&lt;name&gt;:https:0</c>, i.e. the <c>services__&lt;name&gt;__http__0</c> environment
/// variables <see cref="AgendaBuddy.ServiceDefaults.Extensions.AddServiceDefaults{TBuilder}"/> already
/// resolves via <c>AddServiceDiscovery()</c> for every service's own outbound <see cref="HttpClient"/>)
/// — never a static <c>appsettings.json</c> cluster file (ARCHITECTURE.md §2/§5), and never a
/// catch-all forward (T-302, threat-model.md): <see cref="_routeSpecs"/> is the whole allowlist, and
/// nothing outside it becomes a <see cref="RouteConfig"/>.
/// </summary>
/// <remarks>
/// <para>
/// YARP re-reads configuration by polling <see cref="IProxyConfig.ChangeToken"/>: on every tick of
/// <see cref="_pollInterval"/> this provider rebuilds a fresh <see cref="ProxyConfigSnapshot"/> from
/// <see cref="IConfiguration"/>'s current values and signals the previous snapshot's change token,
/// which makes YARP call <see cref="GetConfig"/> again and re-resolve destinations. This is the
/// mechanism ARCHITECTURE.md §6 needed proved: whether a destination's dynamically reassigned port
/// (Aspire restarting a backend resource) is picked up without the Gateway process itself restarting.
/// </para>
/// <para>
/// <b>Spike finding, recorded in ARCHITECTURE.md §6:</b> a destination address never actually goes
/// stale under this project's <c>WithReference</c> wiring — Aspire's DCP fronts every
/// <c>WithReference</c>-injected address with its own stable local proxy port, one level below
/// <see cref="IConfiguration"/>, and re-points that proxy internally on a restart. The polling logic
/// here is kept anyway (it costs nothing, and is the correct defense if that behavior ever changes),
/// but §6 is explicit that T03 should not budget engineering time toward a more aggressive
/// invalidation strategy — the 2-second poll is already more than sufficient headroom.
/// </para>
/// </remarks>
public sealed class AspireServiceDiscoveryProxyConfigProvider : IProxyConfigProvider, IDisposable
{
    /// <summary>
    /// How often this provider re-reads <see cref="IConfiguration"/> and re-signals YARP's change
    /// token. ARCHITECTURE.md §6's spike found this headroom already more than sufficient — Aspire's
    /// DCP proxy absorbs a destination's dynamic-port reassignment one level below
    /// <see cref="IConfiguration"/>, so a more aggressive interval buys nothing for this project's
    /// resource-to-resource references. Kept unchanged from the spike rather than re-tuned.
    /// </summary>
    private static readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// One entry per route this gateway serves — the explicit <c>api/v1/{service}/**</c> allowlist
    /// T-302 (threat-model.md) requires. <b>Never a catch-all forward:</b> a path outside every
    /// pattern here gets no <see cref="RouteConfig"/> at all, so YARP cannot match it — Program.cs's
    /// <c>MapFallback</c> is what turns that "no match" into the shaped <c>gateway-no-route</c> 404.
    /// </summary>
    /// <remarks>
    /// Path prefixes verified against <c>docs/pdlc/context/01-api-surface.md</c>, not assumed from the
    /// logical/Aspire resource name — <c>customer</c>, <c>provider</c> and <c>profession</c> map to the
    /// *plural* route groups (<c>customers</c>, <c>providers</c>, <c>professions</c>) each service
    /// actually registers. Identity is the only service with two entries: its own
    /// <c>api/v1/auth/**</c> group, plus the one route in the whole solution mapped outside the
    /// <c>api/v1/</c> convention — <c>POST /device-token</c>, registered on the app root
    /// (<c>Identity/Program.cs:154</c>), not under the <c>auth</c> group.
    /// </remarks>
    private static readonly (string ServiceName, string RouteId, string PathPattern)[] _routeSpecs =
    [
        ("booking", "booking", "/api/v1/booking/{**catch-all}"),
        ("calendar", "calendar", "/api/v1/calendar/{**catch-all}"),
        ("customer", "customer", "/api/v1/customers/{**catch-all}"),
        ("provider", "provider", "/api/v1/providers/{**catch-all}"),
        ("services", "services", "/api/v1/services/{**catch-all}"),
        ("profession", "profession", "/api/v1/professions/{**catch-all}"),
        ("identity", "identity-auth", "/api/v1/auth/{**catch-all}"),
        ("identity", "identity-device-token", "/device-token"),
        // Found live at F-015-T14's closing verification: messages/notifications are two new
        // TOP-LEVEL route groups on Customer (ADR-036), not children of /api/v1/customers/**, so
        // the "customer" entry above never matched them. MobileApp's Messaging/Notifications
        // screens were unreachable through the gateway despite every other check passing.
        ("customer", "customer-messages", "/api/v1/messages/{**catch-all}"),
        ("customer", "customer-notifications", "/api/v1/notifications/{**catch-all}"),
    ];

    /// <summary>The distinct logical/Aspire resource names <see cref="_routeSpecs"/> covers.</summary>
    private static readonly string[] _destinationNames =
        _routeSpecs.Select(spec => spec.ServiceName).Distinct(StringComparer.Ordinal).ToArray();

    private readonly IConfiguration _configuration;
    private readonly ILogger<AspireServiceDiscoveryProxyConfigProvider> _logger;
    private readonly Timer _timer;
    private volatile ProxyConfigSnapshot _snapshot;

    public AspireServiceDiscoveryProxyConfigProvider(
        IConfiguration configuration,
        ILogger<AspireServiceDiscoveryProxyConfigProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _snapshot = BuildSnapshot();
        _timer = new Timer(_ => Refresh(), state: null, _pollInterval, _pollInterval);
    }

    /// <summary>Returns the current snapshot. Called by YARP whenever the change token fires.</summary>
    public IProxyConfig GetConfig() => _snapshot;

    public void Dispose() => _timer.Dispose();

    /// <summary>
    /// Re-reads <see cref="IConfiguration"/>, swaps in a new snapshot, and signals the old one's
    /// change token so YARP knows to call <see cref="GetConfig"/> again.
    /// </summary>
    private void Refresh()
    {
        var previous = _snapshot;
        var next = BuildSnapshot();

        foreach (var name in _destinationNames)
        {
            var previousAddress = previous.DestinationAddresses.GetValueOrDefault(name);
            var nextAddress = next.DestinationAddresses.GetValueOrDefault(name);

            if (previousAddress != nextAddress)
            {
                _logger.LogInformation(
                    "Aspire service discovery reported a new '{Name}' destination address: {Previous} -> {Next}",
                    name,
                    previousAddress,
                    nextAddress);
            }
        }

        _snapshot = next;
        previous.SignalChange();
    }

    /// <summary>
    /// Builds one <see cref="RouteConfig"/> per <see cref="_routeSpecs"/> entry and one
    /// <see cref="ClusterConfig"/> per distinct service name, resolving each destination's current
    /// address straight from <see cref="IConfiguration"/> — the same <c>services:&lt;name&gt;:http:0</c>
    /// / <c>:https:0</c> keys <c>AddServiceDiscovery()</c> reads. A service whose address cannot be
    /// resolved contributes no route at all (never a route pointing nowhere) — its path(s) fall through
    /// to Program.cs's <c>MapFallback</c> exactly like any other unmapped path, until the address
    /// appears.
    /// </summary>
    private ProxyConfigSnapshot BuildSnapshot()
    {
        var routes = new List<RouteConfig>();
        var clusters = new Dictionary<string, ClusterConfig>(StringComparer.Ordinal);
        var destinationAddresses = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var name in _destinationNames)
        {
            destinationAddresses[name] = ResolveDestinationAddress(name);
        }

        foreach (var spec in _routeSpecs)
        {
            var address = destinationAddresses[spec.ServiceName];

            if (address is null)
            {
                _logger.LogWarning(
                    "No 'services:{Name}:http:0' or 'services:{Name}:https:0' configuration key found; " +
                    "route '{RouteId}' will not be registered until it appears.",
                    spec.ServiceName,
                    spec.ServiceName,
                    spec.RouteId);
                continue;
            }

            routes.Add(new RouteConfig
            {
                RouteId = spec.RouteId,
                ClusterId = spec.ServiceName,
                Match = new RouteMatch { Path = spec.PathPattern }
            });

            if (!clusters.ContainsKey(spec.ServiceName))
            {
                clusters[spec.ServiceName] = new ClusterConfig
                {
                    ClusterId = spec.ServiceName,
                    Destinations = new Dictionary<string, DestinationConfig>(StringComparer.Ordinal)
                    {
                        [$"{spec.ServiceName}-1"] = new DestinationConfig { Address = address }
                    }
                };
            }
        }

        return new ProxyConfigSnapshot(routes, clusters.Values.ToList(), destinationAddresses);
    }

    /// <summary>
    /// Reads the current address for a logical service name, preferring https then falling back to
    /// http, matching how <c>AddServiceDiscovery()</c>'s configuration-based resolver prioritizes
    /// schemes.
    /// </summary>
    private string? ResolveDestinationAddress(string serviceName) =>
        _configuration[$"services:{serviceName}:https:0"]
        ?? _configuration[$"services:{serviceName}:http:0"];

    /// <summary>
    /// An immutable point-in-time route/cluster table plus the change token YARP subscribes to for
    /// the next reload. <see cref="DestinationAddresses"/> is kept alongside purely so
    /// <see cref="Refresh"/> can log when any one of them actually changes.
    /// </summary>
    private sealed class ProxyConfigSnapshot : IProxyConfig
    {
        private readonly CancellationTokenSource _cts = new();

        public ProxyConfigSnapshot(
            IReadOnlyList<RouteConfig> routes,
            IReadOnlyList<ClusterConfig> clusters,
            IReadOnlyDictionary<string, string?> destinationAddresses)
        {
            Routes = routes;
            Clusters = clusters;
            DestinationAddresses = destinationAddresses;
            ChangeToken = new CancellationChangeToken(_cts.Token);
        }

        public IReadOnlyList<RouteConfig> Routes { get; }

        public IReadOnlyList<ClusterConfig> Clusters { get; }

        public IChangeToken ChangeToken { get; }

        public IReadOnlyDictionary<string, string?> DestinationAddresses { get; }

        /// <summary>Fires the change token, telling YARP a fresher snapshot is available.</summary>
        public void SignalChange() => _cts.Cancel();
    }
}
