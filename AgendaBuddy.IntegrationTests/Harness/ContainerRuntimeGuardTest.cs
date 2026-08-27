using Testcontainers.MongoDb;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// Guards the two facts the harness rests on: a MongoDB container starts against the local Docker
/// socket, and the SSH.NET code path is never loaded while doing so.
/// </summary>
/// <remarks>
/// <para>
/// The second assertion is load-bearing rather than decorative. <c>Testcontainers</c> depends on
/// <c>SSH.NET</c>, which carries advisory <c>GHSA-q939-rpr3-3284</c> (HIGH) with **no patched
/// version published** — 2023.0.0 through 2025.0.0 are all flagged, so the advisory cannot be
/// resolved by pinning. SSH.NET exists in the graph only to support Docker-over-SSH, which this
/// project does not use: it talks to a local socket (Rancher Desktop).
/// </para>
/// <para>
/// This test is the evidence for that claim. If someone later configures a remote Docker host over
/// SSH, SSH.NET becomes reachable, the advisory starts describing code this solution executes, and
/// <em>this test fails</em> — rather than the risk changing silently. See ADR-030.
/// </para>
/// <para>
/// Measured on the maintainer's machine 2026-08-18 (Rancher Desktop, 2 CPUs / 4.1 GB, k8s running):
/// <b>cold start 62 s</b> (dominated by the 1.13 GB <c>mongo:7.0</c> pull), <b>warm start 3 s</b>.
/// The warm figure is better than the 4.45 s previously measured, so ADR-017's
/// container-per-class decision holds with margin. The cold figure is a CI consideration for the
/// integration job — image caching is worth roughly a minute per cold runner.
/// </para>
/// </remarks>
public class ContainerRuntimeGuardTest
{
    [Fact]
    public async Task MongoContainer_StartsAgainstLocalDocker_WithoutLoadingSshNet()
    {
        // Without this line, a stopped container runtime makes THIS test the
        // unexplained stall the preflight exists to prevent — it was the first container start in the
        // harness and had no guard in front of it.
        DockerPreflight.EnsureAvailable();

        var container = new MongoDbBuilder().WithImage("mongo:7.0").Build();

        await container.StartAsync();
        try
        {
            var connectionString = container.GetConnectionString();

            Assert.Contains("mongodb://", connectionString);
            Assert.DoesNotContain("mongodb+srv://", connectionString);

            var sshAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetName().Name)
                .Where(name => name is not null && name.Contains("SshNet", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.Empty(sshAssemblies);
        }
        finally
        {
            await container.DisposeAsync();
        }
    }
}
