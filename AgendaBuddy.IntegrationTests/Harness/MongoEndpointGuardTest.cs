namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// Pins the fail-closed guard.
/// </summary>
/// <remarks>
/// <para>
/// Why this matters more than its line count suggests: this repository is <b>public</b>, a valid Atlas
/// credential is still recoverable from its git history (<c>ISSUE-002</c>, unrotated), and that cluster
/// has <b>no backups</b>. An integration suite that resolved a non-container connection string would
/// run destructive setup against live data.
/// </para>
/// <para>
/// The tests are named <c>T002_*</c> so the threat → mitigation → test chain is greppable.
/// </para>
/// </remarks>
public class MongoEndpointGuardTest
{
    private const string Container = "mongodb://127.0.0.1:32768/?directConnection=true";
    private const string Source = "the ConnectionStrings__mongodb environment variable";

    [Fact]
    public void T002_RejectsAnEndpointThatIsNotTheFixturesOwnContainer()
    {
        var exception = Assert.Throws<UnsafeMongoEndpointException>(
            () => MongoEndpointGuard.AssertTargetsContainer(
                resolved: "mongodb://prod-cluster.example.com:27017",
                containerEndpoint: Container,
                source: Source));

        // AC-20 requires the message to NAME the offending host.
        Assert.Contains("prod-cluster.example.com", exception.Message, StringComparison.Ordinal);
        Assert.Contains(Source, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void T002_RejectsALocalhostEndpointOnAnotherPort_NotJustRemoteHosts()
    {
        // THE test that distinguishes this guard from the version Pulse broke at the threat party.
        // A `localhost`/`127.0.0.1` pattern check would wave this through, but reaching Atlas via
        // `kubectl port-forward` or an SSH tunnel presents as exactly this — and a developer may
        // legitimately run their own Mongo locally too. Identity, not hostname shape.
        var exception = Assert.Throws<UnsafeMongoEndpointException>(
            () => MongoEndpointGuard.AssertTargetsContainer(
                resolved: "mongodb://127.0.0.1:27017",
                containerEndpoint: Container,
                source: Source));

        Assert.Contains("27017", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void T002_AcceptsTheFixturesOwnContainerEndpoint()
    {
        MongoEndpointGuard.AssertTargetsContainer(Container, Container, Source);
    }

    [Fact]
    public void T002_AcceptsTheSameContainerDescribedWithDifferentOptions()
    {
        // Identity is host and port, not the literal string. The container currently holds that
        // ephemeral port bound, so nothing else can be listening on it — which is what makes host+port
        // a sound identity rather than a convenient approximation. Tolerating option differences keeps
        // the guard from failing on a harmless formatting variation, which is how fail-closed guards
        // get disabled in practice.
        MongoEndpointGuard.AssertTargetsContainer(
            resolved: "mongodb://127.0.0.1:32768/?retryWrites=false",
            containerEndpoint: Container,
            source: Source);
    }

    /// <summary>
    /// Supplied through <c>MemberData</c> rather than <c>InlineData</c> because these are composed at
    /// runtime — see <see cref="HostileEndpoints"/> for why they cannot be literals.
    /// </summary>
    public static TheoryData<string> ConclusivelyHostileEndpoints() => new()
    {
        HostileEndpoints.Srv(),
        HostileEndpoints.WithCredentials(),
        HostileEndpoints.SrvWithCredentials(),
    };

    [Theory]
    [MemberData(nameof(ConclusivelyHostileEndpoints))]
    public void T002_RejectsSrvAndCredentialBearingEndpointsOutright(string hostile)
    {
        // The cheap second layer, deliberately checkable BEFORE a container exists: no Testcontainer
        // is ever addressed by an SRV record or needs a username, so either is conclusive on its own.
        // Running first means an Atlas string aborts the suite without even pulling an image.
        Assert.Throws<UnsafeMongoEndpointException>(
            () => MongoEndpointGuard.AssertNotObviouslyRemote(hostile, Source));
    }

    [Fact]
    public void T002_NeverEchoesACredentialIntoItsOwnErrorMessage()
    {
        // A guard that prints the string it rejected would write an Atlas password into CI logs while
        // congratulating itself on preventing a leak.
        var exception = Assert.Throws<UnsafeMongoEndpointException>(
            () => MongoEndpointGuard.AssertNotObviouslyRemote(HostileEndpoints.WithCredentials(), Source));

        Assert.DoesNotContain(
            HostileEndpoints.FakePasswordToken, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void T002_AcceptsAnAbsentEndpoint_BecauseTheFixtureIsAboutToSupplyOne()
    {
        // Nothing resolved means nothing to leak to. The fixture sets the container endpoint itself
        // immediately afterwards; the guard's job is to catch a CONFLICTING pre-existing value.
        MongoEndpointGuard.AssertNotObviouslyRemote(null, Source);
    }
}
