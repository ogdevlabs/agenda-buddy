namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// With the container runtime unreachable, the harness fails with a message that
/// names the runtime problem and the remedy — not a bare timeout.
/// </summary>
/// <remarks>
/// <para>
/// Docker cannot be uninstalled inside a test, so the diagnostic is verified by driving
/// <see cref="DockerPreflight.Describe"/> and <see cref="DockerPreflight.Inspect"/> with synthetic
/// inputs. That split is the point of the design: if probing and diagnosing were one method that
/// shelled out and threw, AC-7 could only ever be asserted, never tested.
/// </para>
/// <para>
/// Two cases run against the real machine
/// (<c>Probe_ResolvesAnEndpointOnThisMachine</c>, <c>EnsureAvailable_DoesNotThrowWhenTheRuntimeIsUp</c>)
/// so the probe cannot pass while being vacuous.
/// </para>
/// </remarks>
public class DockerPreflightTest
{
    private const string RancherSocket = "unix:///Users/example/.rd/docker.sock";

    [Fact]
    public void Describe_WhenTheSocketIsMissing_NamesTheEndpointItsSourceAndTheFailure()
    {
        var probe = new ContainerRuntimeProbe(
            IsAvailable: false,
            Endpoint: RancherSocket,
            EndpointSource: "the 'rancher-desktop' docker context",
            Failure: "the socket file does not exist");

        var message = DockerPreflight.Describe(probe);

        Assert.Contains(RancherSocket, message, StringComparison.Ordinal);
        Assert.Contains("rancher-desktop", message, StringComparison.Ordinal);
        Assert.Contains("the socket file does not exist", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_WhenNoEndpointCouldBeResolved_NamesEverywhereItLooked()
    {
        var probe = new ContainerRuntimeProbe(
            IsAvailable: false,
            Endpoint: null,
            EndpointSource: "no source",
            Failure: "no container runtime endpoint could be resolved");

        var message = DockerPreflight.Describe(probe);

        Assert.Contains("DOCKER_HOST", message, StringComparison.Ordinal);
        Assert.Contains("docker context", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RancherSocket, "the socket file does not exist")]
    [InlineData(null, "no container runtime endpoint could be resolved")]
    public void Describe_AlwaysOffersARemedy_NotJustAFailure(string? endpoint, string failure)
    {
        // The whole of AC-7: "names the runtime problem AND the remedy — not a bare timeout."
        var message = DockerPreflight.Describe(
            new ContainerRuntimeProbe(false, endpoint, "a source", failure));

        Assert.Contains("Remed", message, StringComparison.Ordinal);
        Assert.Contains("docker context inspect", message, StringComparison.Ordinal);
        Assert.Contains("DOCKER_HOST=", message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureAvailable_WhenTheRuntimeIsUnavailable_ThrowsNamingTheProblem()
    {
        var probe = new ContainerRuntimeProbe(
            IsAvailable: false,
            Endpoint: RancherSocket,
            EndpointSource: "the 'rancher-desktop' docker context",
            Failure: "the socket file does not exist");

        var exception = Assert.Throws<ContainerRuntimeUnavailableException>(
            () => DockerPreflight.EnsureAvailable(probe));

        Assert.Contains(RancherSocket, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Inspect_WhenTheUnixSocketDoesNotExist_ReportsUnavailableAndSaysWhy()
    {
        var probe = DockerPreflight.Inspect(
            "unix:///var/run/definitely-not-a-real-docker.sock",
            "a synthetic endpoint");

        Assert.False(probe.IsAvailable);
        Assert.NotNull(probe.Failure);
        Assert.Contains("socket", probe.Failure!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inspect_WhenNoEndpointIsGiven_ReportsUnavailable()
    {
        var probe = DockerPreflight.Inspect(endpoint: null, endpointSource: "no source");

        Assert.False(probe.IsAvailable);
        Assert.NotNull(probe.Failure);
    }

    [Fact]
    public void Inspect_WhenTheEndpointCannotBeVerified_DoesNotReportUnavailable()
    {
        // The load-bearing invariant. This preflight exists to turn an opaque hang into a sentence,
        // so it must never be the reason a working suite stops running. A remote or named-pipe
        // endpoint cannot be checked cheaply, and "cannot verify" is not "unreachable" — a false
        // positive here is strictly worse than the hang it replaces.
        var probe = DockerPreflight.Inspect("tcp://192.0.2.1:2375", "a synthetic remote endpoint");

        Assert.True(probe.IsAvailable);
    }

    [Fact]
    public void Probe_ResolvesAnEndpointOnThisMachine()
    {
        // Keeps the probe honest: without this, every assertion above could pass while endpoint
        // resolution silently returned nothing.
        var probe = DockerPreflight.Probe();

        Assert.False(string.IsNullOrWhiteSpace(probe.Endpoint));
        Assert.False(string.IsNullOrWhiteSpace(probe.EndpointSource));
    }

    [Fact]
    public void EnsureAvailable_DoesNotThrowWhenTheRuntimeIsUp()
    {
        // If this fails, the container runtime really is down — and the message tells you what to do,
        // which is the entire point of the task.
        DockerPreflight.EnsureAvailable();
    }
}
