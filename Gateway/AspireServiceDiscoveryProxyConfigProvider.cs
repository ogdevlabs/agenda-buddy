using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace Gateway;

/// <summary>
/// F-015-T02 spike deliverable. Builds YARP's route/cluster table by reading the same Aspire
/// service-discovery configuration keys (<c>services:&lt;name&gt;:http:0</c> /
/// <c>services:&lt;name&gt;:https:0</c>, i.e. the <c>services__&lt;name&gt;__http__0</c> environment
/// variables <see cref="AgendaBuddy.ServiceDefaults.Extensions.AddServiceDefaults{TBuilder}"/> already
/// resolves via <c>AddServiceDiscovery()</c> for every service's own outbound <see cref="HttpClient"/>)
/// — never a static <c>appsettings.json</c> cluster file (ARCHITECTURE.md §2/§5).
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
/// <b>Spike finding, recorded in ARCHITECTURE.md §6:</b> polling alone is necessary but not
/// sufficient. Aspire's <c>WithReference</c> injects the destination address as a plain process
/// environment variable at the Gateway's own launch. The .NET environment-variables configuration
/// provider reads the OS environment block exactly once, at process start — a running process's
/// environment cannot be externally mutated, so <see cref="IConfiguration"/>'s in-process value for
/// <c>services:booking:http:0</c> is frozen for the Gateway's lifetime no matter how often this
/// provider polls it. See the measurement and evidence in ARCHITECTURE.md §6 for what was actually
/// observed running a live AppHost.
/// </para>
/// </remarks>
public sealed class AspireServiceDiscoveryProxyConfigProvider : IProxyConfigProvider, IDisposable
{
    /// <summary>
    /// How often this provider re-reads <see cref="IConfiguration"/> and re-signals YARP's change
    /// token. Short enough for a spike measurement; not tuned for production (that's F-015-T03).
    /// </summary>
    private static readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(2);

    /// <summary>The logical service names this minimal spike proxies to. T03 covers all seven.</summary>
    private static readonly string[] _destinationNames = ["booking"];

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

        if (next.DestinationAddress != previous.DestinationAddress)
        {
            _logger.LogInformation(
                "Aspire service discovery reported a new 'booking' destination address: {Previous} -> {Next}",
                previous.DestinationAddress,
                next.DestinationAddress);
        }

        _snapshot = next;
        previous.SignalChange();
    }

    /// <summary>
    /// Builds one route/cluster pair per entry in <see cref="_destinationNames"/>, resolving each
    /// destination's current address straight from <see cref="IConfiguration"/> — the same
    /// <c>services:&lt;name&gt;:http:0</c> / <c>:https:0</c> keys <c>AddServiceDiscovery()</c> reads.
    /// </summary>
    private ProxyConfigSnapshot BuildSnapshot()
    {
        var routes = new List<RouteConfig>();
        var clusters = new List<ClusterConfig>();
        string? firstDestinationAddress = null;

        foreach (var name in _destinationNames)
        {
            var address = ResolveDestinationAddress(name);
            firstDestinationAddress ??= address;

            if (address is null)
            {
                _logger.LogWarning(
                    "No 'services:{Name}:http:0' or 'services:{Name}:https:0' configuration key found; " +
                    "route for '{Name}' will 503 until it appears.",
                    name,
                    name,
                    name);
                continue;
            }

            routes.Add(new RouteConfig
            {
                RouteId = name,
                ClusterId = name,
                Match = new RouteMatch { Path = $"/api/v1/{name}/{{**catch-all}}" }
            });

            clusters.Add(new ClusterConfig
            {
                ClusterId = name,
                Destinations = new Dictionary<string, DestinationConfig>(StringComparer.Ordinal)
                {
                    [$"{name}-1"] = new DestinationConfig { Address = address }
                }
            });
        }

        return new ProxyConfigSnapshot(routes, clusters, firstDestinationAddress);
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
    /// the next reload. <see cref="DestinationAddress"/> is kept alongside purely so
    /// <see cref="Refresh"/> can log when it actually changes.
    /// </summary>
    private sealed class ProxyConfigSnapshot : IProxyConfig
    {
        private readonly CancellationTokenSource _cts = new();

        public ProxyConfigSnapshot(
            IReadOnlyList<RouteConfig> routes,
            IReadOnlyList<ClusterConfig> clusters,
            string? destinationAddress)
        {
            Routes = routes;
            Clusters = clusters;
            DestinationAddress = destinationAddress;
            ChangeToken = new CancellationChangeToken(_cts.Token);
        }

        public IReadOnlyList<RouteConfig> Routes { get; }

        public IReadOnlyList<ClusterConfig> Clusters { get; }

        public IChangeToken ChangeToken { get; }

        public string? DestinationAddress { get; }

        /// <summary>Fires the change token, telling YARP a fresher snapshot is available.</summary>
        public void SignalChange() => _cts.Cancel();
    }
}
