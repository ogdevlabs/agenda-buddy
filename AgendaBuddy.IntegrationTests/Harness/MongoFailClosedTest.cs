using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// F-016 AC-20 (`[security]`, threat <b>T-002</b>, CRITICAL) at the fixture level: a conflicting
/// connection string in the environment aborts the suite before any test body runs, and no database or
/// collection is created.
/// </summary>
/// <remarks>
/// <para>
/// <c>MongoEndpointGuardTest</c> pins the decision logic with synthetic inputs. This pins that the
/// decision is actually <b>wired into the fixture's construction path</b> — a correct guard nobody calls
/// is the failure mode that would leave AC-20 looking satisfied.
/// </para>
/// <para>
/// This class mutates <c>ConnectionStrings__mongodb</c> process-wide, which is safe only because
/// <see cref="HarnessCollection"/> disables parallelization. It restores the previous value in a
/// <c>finally</c>.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class MongoFailClosedTest(CryptoSessionFixture crypto)
{
    private const string ConnectionStringVariable = "ConnectionStrings__mongodb";

    [Fact]
    public async Task T002_AbortsDuringFixtureConstruction_AndCreatesNoDatabase()
    {
        // Deliberately localhost on a DIFFERENT port: parseable, credential-free and not an SRV record,
        // so it survives the cheap layer and can only be caught by the identity check. This is the
        // shape a `kubectl port-forward` or SSH tunnel to Atlas presents as.
        var original = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        try
        {
            Environment.SetEnvironmentVariable(ConnectionStringVariable, "mongodb://127.0.0.1:27017");

            var fixture = new ServiceHostFixture<ProfessionAnchor>(crypto);

            var exception = await Assert.ThrowsAsync<UnsafeMongoEndpointException>(
                () => fixture.InitializeAsync());

            Assert.Contains("27017", exception.Message, StringComparison.Ordinal);

            // AC-20's second half, asserted positively rather than by absence (wave-4 standup, E-3):
            // connect to the container the fixture did start and prove no test database exists in it.
            Assert.NotNull(fixture.ContainerConnectionString);

            var databases = await new MongoClient(fixture.ContainerConnectionString)
                .ListDatabaseNames()
                .ToListAsync();

            Assert.DoesNotContain(
                databases,
                name => name.StartsWith("itest_", StringComparison.Ordinal));

            await fixture.DisposeAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConnectionStringVariable, original);
        }
    }

    [Fact]
    public async Task T002_AbortsBeforeStartingAnyContainer_WhenTheEnvironmentNamesAnAtlasCluster()
    {
        // The ordering claim in MongoEndpointGuard's remarks, made verifiable: an SRV endpoint is
        // conclusive without a container to compare against, so the run must abort BEFORE a 1.13 GB
        // image is pulled. ContainerConnectionString staying null is the evidence.
        var original = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                ConnectionStringVariable,
                "mongodb+srv://agenda_buddy:s3cret@cluster0.abcde.mongodb.net/agenda_buddy");

            var fixture = new ServiceHostFixture<ProfessionAnchor>(crypto);

            var exception = await Assert.ThrowsAsync<UnsafeMongoEndpointException>(
                () => fixture.InitializeAsync());

            Assert.Null(fixture.ContainerConnectionString);
            Assert.DoesNotContain("s3cret", exception.Message, StringComparison.Ordinal);

            await fixture.DisposeAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConnectionStringVariable, original);
        }
    }

    [Fact]
    public async Task T002_StartsNormallyWhenTheEnvironmentIsClean()
    {
        // The guard must not be a suite-wide outage. Without this, a guard that rejected everything
        // would pass both tests above and nobody would notice until the whole harness stopped running.
        var original = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        try
        {
            Environment.SetEnvironmentVariable(ConnectionStringVariable, null);

            var fixture = new ServiceHostFixture<ProfessionAnchor>(crypto);
            await fixture.InitializeAsync();

            Assert.NotNull(fixture.ContainerConnectionString);

            await fixture.DisposeAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConnectionStringVariable, original);
        }
    }
}
