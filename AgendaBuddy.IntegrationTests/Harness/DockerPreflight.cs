using System.Net.Sockets;
using System.Text.Json;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// What the preflight found when it looked for a container runtime.
/// </summary>
/// <param name="IsAvailable">
/// <c>false</c> only on <b>positive evidence</b> of unreachability. An endpoint the preflight cannot
/// cheaply check reports <c>true</c> — see <see cref="DockerPreflight.Inspect"/>.
/// </param>
/// <param name="Endpoint">The resolved endpoint URI, or <c>null</c> if none could be resolved.</param>
/// <param name="EndpointSource">Where the endpoint came from, in words, for the diagnostic.</param>
/// <param name="Failure">Why it is unreachable, in words. <c>null</c> when available.</param>
internal sealed record ContainerRuntimeProbe(
    bool IsAvailable,
    string? Endpoint,
    string EndpointSource,
    string? Failure);

/// <summary>
/// Thrown instead of letting the container runtime fail as an opaque timeout.
/// </summary>
internal sealed class ContainerRuntimeUnavailableException(string message) : Exception(message);

/// <summary>
/// Turns "the container runtime is unreachable" from an unexplained hang into a sentence that names
/// the endpoint, where that endpoint came from, what is wrong with it, and what to do about it.
/// </summary>
/// <remarks>
/// <para>
/// Absent this, the most likely local failure in the whole harness
/// presents as a stall with no output.
/// </para>
/// <para>
/// <b>Probing is separated from diagnosing on purpose.</b> Docker cannot be uninstalled inside a
/// test, so a single method that shells out and throws would leave AC-7 asserted but unverified.
/// <see cref="Describe"/> is a pure function of <see cref="ContainerRuntimeProbe"/> and
/// <see cref="Inspect"/> takes its endpoint as an argument, so both are driven with synthetic inputs
/// by <c>DockerPreflightTest</c>.
/// </para>
/// <para>
/// <b>Resolution order matches Testcontainers.NET</b>, because a preflight that checks a different
/// endpoint than the library it guards is worse than none: <c>DOCKER_HOST</c>, then
/// <c>docker.host</c> in <c>~/.testcontainers.properties</c>, then the current docker context, then
/// the default Unix socket. On the maintainer's machine that resolves through the context —
/// <c>~/.docker/config.json</c> names <c>rancher-desktop</c>, whose endpoint is
/// <c>unix:///Users/&lt;user&gt;/.rd/docker.sock</c>. Note that <c>/var/run/docker.sock</c> does
/// <b>not</b> exist there, so a preflight hardcoded to the default socket would have reported a false
/// failure on the one machine this feature is built on.
/// </para>
/// <para>
/// ⚠️ <b>It never blocks on uncertainty.</b> Only a resolvable-but-broken Unix socket is treated as
/// unavailable. A <c>tcp://</c> or <c>npipe://</c> endpoint reports available-by-default, because the
/// cost of a false positive — a working suite refusing to run — is worse than the hang this exists to
/// explain. Residual, stated rather than hidden: if the socket exists and something is listening but
/// the daemon itself is wedged, this passes and Testcontainers' own error surfaces. Detecting that
/// needs a real API call with its own timeout budget, which is not worth building for one criterion.
/// </para>
/// </remarks>
internal static class DockerPreflight
{
    private const string DefaultUnixSocket = "unix:///var/run/docker.sock";
    private const string UnixScheme = "unix://";

    /// <summary>
    /// Throws <see cref="ContainerRuntimeUnavailableException"/> if no container runtime can be
    /// reached. Call before starting any container.
    /// </summary>
    public static void EnsureAvailable() => EnsureAvailable(Probe());

    /// <summary>Overload taking a probe, so the throw path is testable.</summary>
    public static void EnsureAvailable(ContainerRuntimeProbe probe)
    {
        if (!probe.IsAvailable)
        {
            throw new ContainerRuntimeUnavailableException(Describe(probe));
        }
    }

    /// <summary>Resolves the endpoint the way Testcontainers does, then inspects it.</summary>
    public static ContainerRuntimeProbe Probe()
    {
        var (endpoint, source) = ResolveEndpoint();
        return Inspect(endpoint, source);
    }

    /// <summary>
    /// Decides whether <paramref name="endpoint"/> is reachable, without any call that can block.
    /// </summary>
    public static ContainerRuntimeProbe Inspect(string? endpoint, string endpointSource)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return new ContainerRuntimeProbe(
                false, null, endpointSource, "no container runtime endpoint could be resolved");
        }

        if (!endpoint.StartsWith(UnixScheme, StringComparison.OrdinalIgnoreCase))
        {
            // Cannot verify cheaply, so do not claim it is broken. See the remarks on this class.
            return new ContainerRuntimeProbe(true, endpoint, endpointSource, null);
        }

        var socketPath = endpoint[UnixScheme.Length..];

        if (!File.Exists(socketPath))
        {
            return new ContainerRuntimeProbe(
                false, endpoint, endpointSource, "the socket file does not exist");
        }

        try
        {
            // Connecting to a Unix domain socket either succeeds or is refused immediately — there is
            // no timeout to wait out, which is what makes this safe to do in a preflight.
            using var probeSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            probeSocket.Connect(new UnixDomainSocketEndPoint(socketPath));
        }
        catch (SocketException exception)
        {
            return new ContainerRuntimeProbe(
                false,
                endpoint,
                endpointSource,
                $"the socket file exists but nothing accepted a connection ({exception.SocketErrorCode})");
        }

        return new ContainerRuntimeProbe(true, endpoint, endpointSource, null);
    }

    /// <summary>The AC-7 message: the problem, where it came from, and the remedy.</summary>
    public static string Describe(ContainerRuntimeProbe probe)
    {
        var located = probe.Endpoint is null
            ? "  Endpoint:  none — nothing to connect to"
            : $"  Endpoint:  {probe.Endpoint}{Environment.NewLine}" +
              $"  Resolved from: {probe.EndpointSource}";

        return string.Join(Environment.NewLine,
            "The container runtime is not reachable, so the integration harness cannot start a database.",
            string.Empty,
            located,
            $"  Problem:   {probe.Failure ?? "unknown"}",
            string.Empty,
            "Remedies, most likely first:",
            "  1. Start the container runtime (Rancher Desktop, Docker Desktop or colima) and wait for it",
            "     to report running.",
            "  2. Check which endpoint the CLI resolves, and compare it to the one above:",
            "       docker context inspect",
            "  3. If the runtime listens somewhere else, point the harness straight at it:",
            "       export DOCKER_HOST=unix:///path/to/docker.sock",
            "  4. Under Rancher Desktop the CLI lives in ~/.rd/bin, which is not on PATH in every shell.",
            "     If `docker` is not found at all:",
            "       export PATH=\"$HOME/.rd/bin:$PATH\"",
            string.Empty,
            "This message replaces the opaque stall the harness would otherwise produce (F-016 AC-7).");
    }

    private static (string? Endpoint, string Source) ResolveEndpoint()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("DOCKER_HOST");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return (fromEnvironment, "the DOCKER_HOST environment variable");
        }

        var fromProperties = ReadTestcontainersDockerHost();
        if (fromProperties is not null)
        {
            return (fromProperties, "docker.host in ~/.testcontainers.properties");
        }

        var fromContext = ReadCurrentDockerContextEndpoint();
        if (fromContext is not null)
        {
            return (fromContext.Value.Endpoint, $"the '{fromContext.Value.ContextName}' docker context");
        }

        return (DefaultUnixSocket, "the default Unix socket");
    }

    /// <summary>
    /// <c>docker.host</c> from <c>~/.testcontainers.properties</c>, which takes precedence over the
    /// docker context in Testcontainers. Handled so a developer who uses that file does not get a
    /// false failure from a preflight looking somewhere else.
    /// </summary>
    private static string? ReadTestcontainersDockerHost()
    {
        var path = Path.Combine(HomeDirectory(), ".testcontainers.properties");
        if (!File.Exists(path))
        {
            return null;
        }

        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("docker.host", StringComparison.OrdinalIgnoreCase)
                && trimmed.Split('=', 2) is [_, var value]
                && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// The endpoint of the context named by <c>~/.docker/config.json</c>. Contexts are stored under a
    /// hash of their name, so the metadata files are enumerated and matched on <c>Name</c> rather than
    /// re-deriving the hash.
    /// </summary>
    private static (string Endpoint, string ContextName)? ReadCurrentDockerContextEndpoint()
    {
        var dockerDirectory = Path.Combine(HomeDirectory(), ".docker");
        var configPath = Path.Combine(dockerDirectory, "config.json");
        var metaDirectory = Path.Combine(dockerDirectory, "contexts", "meta");

        if (!File.Exists(configPath) || !Directory.Exists(metaDirectory))
        {
            return null;
        }

        string? currentContext;
        try
        {
            using var config = JsonDocument.Parse(File.ReadAllText(configPath));
            currentContext = config.RootElement.TryGetProperty("currentContext", out var value)
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(currentContext))
        {
            return null;
        }

        foreach (var metaPath in Directory.EnumerateFiles(metaDirectory, "meta.json", SearchOption.AllDirectories))
        {
            try
            {
                using var meta = JsonDocument.Parse(File.ReadAllText(metaPath));
                var root = meta.RootElement;

                if (!root.TryGetProperty("Name", out var name)
                    || name.GetString() != currentContext
                    || !root.TryGetProperty("Endpoints", out var endpoints)
                    || !endpoints.TryGetProperty("docker", out var docker)
                    || !docker.TryGetProperty("Host", out var host)
                    || host.GetString() is not { Length: > 0 } endpoint)
                {
                    continue;
                }

                return (endpoint, currentContext);
            }
            catch (JsonException)
            {
                // A malformed context file is not a reason to give up on the others.
            }
        }

        return null;
    }

    private static string HomeDirectory() =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
}
