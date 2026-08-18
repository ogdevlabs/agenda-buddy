using Library.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// One MongoDB container per test class, and a real service hosted over HTTP against it.
/// </summary>
/// <typeparam name="TEntryPoint">
/// Any public type from the service assembly — see <see cref="EntryPoints"/> for why it is not
/// <c>Program</c>.
/// </typeparam>
/// <remarks>
/// <para>
/// F-016 AC-4, AC-5 and AC-20. Use as a class fixture on a test class that is also in
/// <see cref="HarnessCollection"/>:
/// </para>
/// <code>
/// [Collection(HarnessCollection.Name)]
/// public class MyServiceTest(ServiceHostFixture&lt;Anchor&gt; host) : IClassFixture&lt;...&gt;
/// </code>
/// <para>
/// <b>Container per class, database per test</b> (ADR-017). F-018's spike measured 4.45 s warm
/// container startup against the 1–3 s originally assumed, which reversed an earlier per-test design;
/// wave 3 measured 3 s warm / 62 s cold on this hardware. Building a
/// <see cref="WebApplicationFactory{TEntryPoint}"/> costs nothing by comparison, so
/// <see cref="StartService"/> gives each test its own database inside the shared container. The Rancher
/// VM is 2 CPUs / 4.1 GB and already runs a k8s cluster — if it thrashes, the mitigation is fewer,
/// larger test classes, not abandoning containers.
/// </para>
/// <para>
/// <b>The fixture never writes <c>ConnectionStrings__mongodb</c>.</b> It injects the container endpoint
/// through <see cref="WebApplicationFactory{TEntryPoint}"/> settings and treats the process environment
/// as strictly read-only. Two reasons, and the second is not obvious: writing it would defeat the guard
/// below (it would end up comparing its own value to itself), and it would poison the *next* test
/// class, which starts a different container on a different port and would read the previous class's
/// endpoint as a conflicting ambient value and abort.
/// </para>
/// <para>
/// <c>JWT_PUBLIC_KEY</c> is the exception and must be an environment variable, because
/// <c>AuthenticationExtensions.cs:16</c> reads it with <c>Environment.GetEnvironmentVariable</c> rather
/// than through <c>IConfiguration</c>, and throws at DI-registration time when it is missing — so it has
/// to be set before the host builds, even for a test that calls an anonymous route. It is one
/// session-constant value from <see cref="CryptoSessionFixture"/>, so unlike a connection string it
/// cannot drift between classes.
/// </para>
/// </remarks>
public class ServiceHostFixture<TEntryPoint>(CryptoSessionFixture crypto) : IAsyncLifetime
    where TEntryPoint : class
{
    private const string AmbientSource = "the ambient environment (ConnectionStrings__mongodb or a "
                                         + "legacy MongoDB connection-string key)";

    private readonly MongoDbContainer _container = new MongoDbBuilder().WithImage("mongo:7.0").Build();
    private readonly List<ServiceHost> _started = [];

    /// <summary>
    /// The container's endpoint, available as soon as it starts — <b>including when the guard then
    /// rejects the run</b>, so a test can connect and prove no database was created.
    /// </summary>
    public string? ContainerConnectionString { get; private set; }

    public async Task InitializeAsync()
    {
        // Ordered deliberately: the cheapest conclusive check first, so a stray Atlas string aborts
        // without pulling a 1.13 GB image, and an unreachable runtime says so instead of stalling.
        MongoEndpointGuard.AssertNotObviouslyRemote(ResolveAmbientConnectionString(), AmbientSource);
        DockerPreflight.EnsureAvailable();

        await _container.StartAsync();
        ContainerConnectionString = _container.GetConnectionString();

        // The identity check, which needs a container to compare against. Nothing has connected to
        // MongoDB yet and no host has been built, so aborting here creates no database or collection.
        var ambient = ResolveAmbientConnectionString();
        if (ambient is not null)
        {
            MongoEndpointGuard.AssertTargetsContainer(ambient, ContainerConnectionString, AmbientSource);
        }

        Environment.SetEnvironmentVariable("JWT_PUBLIC_KEY", crypto.PublicKeyPem);
    }

    /// <summary>
    /// Starts the service against a fresh, uniquely named database in this class's container.
    /// </summary>
    public ServiceHost StartService()
    {
        if (ContainerConnectionString is null)
        {
            throw new InvalidOperationException(
                $"{nameof(InitializeAsync)} has not completed — the container is not running.");
        }

        var databaseName = $"itest_{Guid.NewGuid():N}";

        var factory = new WebApplicationFactory<TEntryPoint>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:mongodb", ContainerConnectionString);

            // MongoDbSettings is the highest-precedence prefix MongoConnectionResolver.ResolveSetting
            // searches (MongoConnectionResolver.cs:22-27), so this overrides each service's
            // LibrarySettings:MongoDB:DatabaseName without editing appsettings.
            builder.UseSetting("MongoDbSettings:DatabaseName", databaseName);
        });

        var host = new ServiceHost(
            factory, factory.CreateClient(), databaseName, ContainerConnectionString);
        _started.Add(host);
        return host;
    }

    public async Task DisposeAsync()
    {
        foreach (var host in _started)
        {
            host.Dispose();
        }

        await _container.DisposeAsync();
    }

    /// <summary>
    /// What a service would resolve from the environment alone, or <c>null</c> if nothing is set.
    /// </summary>
    /// <remarks>
    /// Goes through <see cref="MongoConnectionResolver"/> rather than reading one variable, because the
    /// resolver searches <b>four</b> keys in priority order and is what the services actually use. All
    /// four appsettings paths are currently the empty string, which <c>Resolve</c> skips — so today the
    /// only live hazard is an environment variable, but checking the resolver keeps that from being an
    /// assumption baked into the guard.
    /// </remarks>
    private static string? ResolveAmbientConnectionString()
    {
        var environmentOnly = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        try
        {
            return MongoConnectionResolver.Resolve(environmentOnly);
        }
        catch (InvalidOperationException)
        {
            // Nothing configured. Safe: there is nothing to leak to, and StartService supplies the
            // container endpoint itself.
            return null;
        }
    }
}

/// <summary>
/// One running service, bound to its own database inside the class's container.
/// </summary>
/// <remarks>
/// <para>
/// <b>What "over HTTP" means here.</b> The client is a real <see cref="HttpClient"/> issuing real HTTP
/// requests through the service's entire pipeline — routing, authentication, authorization, model
/// binding, the exception handler — against real MongoDB. The transport underneath is
/// <c>TestServer</c>'s in-memory one rather than a TCP socket, which is what
/// <c>Microsoft.AspNetCore.Mvc.Testing</c> provides and what this project's design selected (see
/// <see cref="EntryPoints"/>). Stated plainly because AC-4 says "real HTTP request": everything above
/// the transport is real, and `11-testing.md:148`'s finding — that no route table in this solution is
/// executed by any test — is what this closes.
/// </para>
/// </remarks>
public sealed class ServiceHost : IDisposable
{
    private readonly IDisposable _factory;

    internal ServiceHost(IDisposable factory, HttpClient client, string databaseName, string connectionString)
    {
        _factory = factory;
        Client = client;
        DatabaseName = databaseName;
        Database = new MongoClient(connectionString).GetDatabase(databaseName);
    }

    /// <summary>A client whose requests traverse the service's full middleware pipeline.</summary>
    public HttpClient Client { get; }

    /// <summary>This test's database, for arranging fixtures and asserting on what was written.</summary>
    public IMongoDatabase Database { get; }

    /// <summary>The unique database name this service was configured with.</summary>
    public string DatabaseName { get; }

    public void Dispose()
    {
        Client.Dispose();
        _factory.Dispose();
    }
}
