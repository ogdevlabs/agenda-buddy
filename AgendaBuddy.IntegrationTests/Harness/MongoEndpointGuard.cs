using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// Thrown when the harness would have talked to a MongoDB endpoint it does not own.
/// </summary>
internal sealed class UnsafeMongoEndpointException(string message) : Exception(message);

/// <summary>
/// The fail-closed guard: the harness refuses to run unless the database it resolves is the container
/// this test session started.
/// </summary>
/// <remarks>
/// <para>
/// F-016 AC-5 and AC-20 (`[security]`, threat <b>T-002</b>, CRITICAL). This repository is
/// <b>public</b>, a valid Atlas credential remains recoverable from its git history
/// (<c>ISSUE-002</c>, unrotated), and that cluster has <b>no backups</b>. A suite that resolved a
/// non-container connection string would run destructive setup against live data. Under no
/// circumstances may an integration test reach a real cluster.
/// </para>
/// <para>
/// <b>Two layers, in this order.</b>
/// </para>
/// <list type="number">
/// <item>
/// <see cref="AssertNotObviouslyRemote"/> — conclusive on its own and needs no container, so it runs
/// <em>before</em> one is started. No Testcontainer is addressed by an SRV record or needs a username,
/// so either is decisive. An Atlas string therefore aborts the suite without even pulling an image.
/// </item>
/// <item>
/// <see cref="AssertTargetsContainer"/> — identity. Compares against the endpoint the Testcontainers
/// API reports for the container this fixture started.
/// </item>
/// </list>
/// <para>
/// ⚠️ <b>A <c>localhost</c> pattern check is explicitly insufficient</b>, and that is not a
/// hypothetical: an earlier version of this guard was broken at the threat-model party on exactly this
/// point. Reaching Atlas through <c>kubectl port-forward</c> or an SSH tunnel presents as
/// <c>127.0.0.1</c>, and a developer may legitimately run their own Mongo locally. So the comparison is
/// <b>host and port</b>, taken from the container's own reported endpoint. That is sound identity
/// rather than a convenient approximation, because the container holds that ephemeral port bound for
/// the duration of the run — nothing else can be listening on it.
/// </para>
/// <para>
/// Comparison is on host and port rather than the literal string, so a harmless option difference does
/// not abort the run. Fail-closed guards that cry wolf get commented out.
/// </para>
/// <para>
/// <b>Nothing here ever echoes the rejected connection string.</b> A guard that printed what it
/// rejected would write an Atlas password into CI logs while congratulating itself on preventing a
/// leak. Only host and port are reported.
/// </para>
/// </remarks>
internal static class MongoEndpointGuard
{
    /// <summary>
    /// Rejects endpoints that cannot possibly be a local Testcontainer, without needing one to exist.
    /// </summary>
    /// <param name="connectionString">
    /// The resolved connection string, or <c>null</c> when nothing is configured yet — which is safe,
    /// because there is nothing to leak to and the fixture is about to supply its own.
    /// </param>
    /// <param name="source">Where the value came from, named in the failure message.</param>
    public static void AssertNotObviouslyRemote(string? connectionString, string source)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        if (connectionString.Contains("mongodb+srv://", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnsafeMongoEndpointException(Refusal(
                "it uses an SRV connection string (mongodb+srv://), which no local container does",
                source,
                DescribeSafely(connectionString)));
        }

        if (HasCredentials(connectionString))
        {
            throw new UnsafeMongoEndpointException(Refusal(
                "it carries credentials, and a local Testcontainer needs none",
                source,
                DescribeSafely(connectionString)));
        }
    }

    /// <summary>
    /// Requires <paramref name="resolved"/> to address the same host and port as
    /// <paramref name="containerEndpoint"/>.
    /// </summary>
    public static void AssertTargetsContainer(string resolved, string containerEndpoint, string source)
    {
        AssertNotObviouslyRemote(resolved, source);

        if (HostAndPort(resolved) == HostAndPort(containerEndpoint))
        {
            return;
        }

        throw new UnsafeMongoEndpointException(Refusal(
            $"it does not address this session's container, which is at {HostAndPort(containerEndpoint)}",
            source,
            DescribeSafely(resolved)));
    }

    private static string Refusal(string reason, string source, string offending) => string.Join(
        Environment.NewLine,
        "The integration harness refused to start: it would have connected to a MongoDB endpoint it "
        + "does not own.",
        string.Empty,
        $"  Offending endpoint: {offending}",
        $"  Configured by:      {source}",
        $"  Refused because:    {reason}",
        string.Empty,
        "This repository is PUBLIC and a valid Atlas credential is still recoverable from its git",
        "history (docs/issues/ISSUE-002, unrotated). That cluster has no backups, so an integration",
        "test that reached it would run destructive setup against live data. The harness fails closed",
        "rather than guessing (F-016 AC-5 / AC-20, threat T-002).",
        string.Empty,
        "If you set a MongoDB connection string in this shell for another purpose, unset it:",
        "  unset ConnectionStrings__mongodb");

    /// <summary>
    /// Host and port only — never the credentials, database or options.
    /// </summary>
    private static string DescribeSafely(string connectionString)
    {
        var hostAndPort = HostAndPort(connectionString);
        return hostAndPort == UnparseableEndpoint
            ? "(unparseable connection string — withheld, it may contain a credential)"
            : hostAndPort;
    }

    private const string UnparseableEndpoint = "(unparseable)";

    private static string HostAndPort(string connectionString)
    {
        try
        {
            var url = new MongoUrl(connectionString);
            return string.Join(",", url.Servers.Select(server => $"{server.Host}:{server.Port}"));
        }
        catch (MongoConfigurationException)
        {
            return UnparseableEndpoint;
        }
        catch (FormatException)
        {
            return UnparseableEndpoint;
        }
    }

    private static bool HasCredentials(string connectionString)
    {
        try
        {
            var url = new MongoUrl(connectionString);
            return !string.IsNullOrEmpty(url.Username);
        }
        catch (Exception exception) when (exception is MongoConfigurationException or FormatException)
        {
            // An endpoint the driver cannot even parse is not one to press on with.
            return true;
        }
    }
}
