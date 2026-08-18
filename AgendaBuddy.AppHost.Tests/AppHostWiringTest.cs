using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Xunit;

namespace AgendaBuddy.AppHost.Tests;

/// <summary>
/// Asserts the shape of the resource graph without starting anything, so no container runtime is
/// needed. Whether the graph actually comes up is AC-1.1, verified manually in T-10.
/// </summary>
public class AppHostWiringTest
{
    private static readonly string[] ExpectedServices =
        ["booking", "calendar", "customer", "identity", "profession", "provider", "services"];

    private static IDistributedApplicationBuilder BuildModel(
        DeploymentTarget target = DeploymentTarget.Local)
    {
        var builder = DistributedApplication.CreateBuilder([]);
        AppHostWiring.Configure(builder, target);
        return builder;
    }

    private static IResource Resource(IDistributedApplicationBuilder builder, string name) =>
        Assert.Single(builder.Resources.Where(resource => resource.Name == name));

    /// <summary>Names of the resources this one was given a <c>WithReference</c> to.</summary>
    private static List<string> References(IDistributedApplicationBuilder builder, string name) =>
        Relationships(builder, name, "Reference");

    /// <summary>Names of the resources this one waits for.</summary>
    private static List<string> Waits(IDistributedApplicationBuilder builder, string name) =>
        Relationships(builder, name, "WaitFor");

    private static List<string> Relationships(
        IDistributedApplicationBuilder builder, string name, string type) =>
        Resource(builder, name).Annotations
            .OfType<ResourceRelationshipAnnotation>()
            .Where(relationship => relationship.Type == type)
            .Select(relationship => relationship.Resource.Name)
            .Distinct()
            .ToList();

    // AC-1.2: the dashboard must list all nine resources, so every service is orchestrated rather
    // than just the ones someone remembered.
    [Fact]
    public void AllNineResourcesAreRegistered()
    {
        var builder = BuildModel();

        foreach (var name in ExpectedServices)
        {
            Assert.IsAssignableFrom<ProjectResource>(Resource(builder, name));
        }

        Assert.IsAssignableFrom<MongoDBServerResource>(Resource(builder, "mongodb"));
        Assert.IsAssignableFrom<KafkaServerResource>(Resource(builder, "kafka"));
    }

    // AC-1.5: a MAUI head has no place in a server orchestration graph.
    [Fact]
    public void MobileAppIsNotRegistered()
    {
        var builder = BuildModel();

        Assert.DoesNotContain(
            builder.Resources,
            resource => resource.Name.Contains("mobile", StringComparison.OrdinalIgnoreCase));
    }

    // The catalog records agenda_buddy for the six domain services and IdentityDb for Identity.
    // Renaming either would silently point services at empty collections.
    [Fact]
    public void BothDatabasesKeepTheirPhysicalNames()
    {
        var builder = BuildModel();

        var agendaDb = Assert.IsAssignableFrom<MongoDBDatabaseResource>(Resource(builder, "agenda-buddy"));
        var identityDb = Assert.IsAssignableFrom<MongoDBDatabaseResource>(Resource(builder, "IdentityDb"));

        // Hyphenated resource name because ASPIRE006 rejects underscores; the physical database
        // name is what the services actually read.
        Assert.Equal("agenda_buddy", agendaDb.DatabaseName);
        Assert.Equal("IdentityDb", identityDb.DatabaseName);
    }

    // Seeded data has to survive a restart, or re-seeding becomes the first step of every session.
    [Fact]
    public void MongoDbHasAPersistentDataVolume()
    {
        var builder = BuildModel();

        Assert.Contains(
            Resource(builder, "mongodb").Annotations.OfType<ContainerMountAnnotation>(),
            mount => mount.Type == ContainerMountType.Volume);
    }

    // Edge case E-10: CreateTopicIfNotExist treats an existing topic as a failure, so a persisted
    // Kafka volume would break topic re-registration on every restart after the first.
    [Fact]
    public void KafkaHasNoDataVolume()
    {
        var builder = BuildModel();

        Assert.DoesNotContain(
            Resource(builder, "kafka").Annotations.OfType<ContainerMountAnnotation>(),
            mount => mount.Type == ContainerMountType.Volume);
    }

    // A-3: only the three services that produce per-provider topics get Kafka.
    [Theory]
    [InlineData("booking")]
    [InlineData("customer")]
    [InlineData("provider")]
    public void KafkaIsReferencedBy(string serviceName)
    {
        Assert.Contains("kafka", References(BuildModel(), serviceName));
    }

    [Theory]
    [InlineData("calendar")]
    [InlineData("services")]
    [InlineData("profession")]
    [InlineData("identity")]
    public void KafkaIsNotReferencedBy(string serviceName)
    {
        Assert.DoesNotContain("kafka", References(BuildModel(), serviceName));
    }

    // Identity owns its own database; the six domain services share agenda_buddy.
    [Fact]
    public void IdentityReferencesOnlyItsOwnDatabase()
    {
        var references = References(BuildModel(), "identity");

        Assert.Contains("IdentityDb", references);
        Assert.DoesNotContain("agenda-buddy", references);
    }

    [Theory]
    [InlineData("booking")]
    [InlineData("calendar")]
    [InlineData("customer")]
    [InlineData("profession")]
    [InlineData("provider")]
    [InlineData("services")]
    public void DomainServiceReferencesTheSharedDatabase(string serviceName)
    {
        var references = References(BuildModel(), serviceName);

        Assert.Contains("agenda-buddy", references);
        Assert.DoesNotContain("IdentityDb", references);
    }

    // AC-1.4: no service may bind a hardcoded localhost:603x port under the AppHost. Each one is
    // pinned twice over — by its launch profile and by Kestrel:Endpoints in appsettings.json — so
    // this fails unless both routes are neutralised.
    [Fact]
    public void NoServiceBindsAHardcodedHostPort()
    {
        var builder = BuildModel();

        foreach (var name in ExpectedServices)
        {
            var endpoints = Resource(builder, name).Annotations.OfType<EndpointAnnotation>().ToList();

            Assert.All(endpoints, endpoint =>
                Assert.False(endpoint.Port is >= 6030 and <= 6039 || endpoint.TargetPort is >= 6030 and <= 6039,
                    $"{name} pinned port {endpoint.Port ?? endpoint.TargetPort}; the AppHost must assign it."));

            Assert.All(endpoints, endpoint =>
            {
                Assert.Null(endpoint.Port);
                Assert.Null(endpoint.TargetPort);
            });
        }
    }

    // Threat T-003: the signing keys must never be committed. Declaring them secret keeps them in
    // user secrets and masked in the dashboard.
    [Theory]
    [InlineData("jwt-public-key")]
    [InlineData("jwt-private-key")]
    public void JwtKeyIsASecretParameter(string parameterName)
    {
        var parameter = Assert.IsAssignableFrom<ParameterResource>(Resource(BuildModel(), parameterName));

        Assert.True(parameter.Secret, $"{parameterName} must be declared secret.");
    }

    // AC-2.3 / ISSUE-001: WithReference injects ConnectionStrings__<resource name>, which is
    // agenda-buddy or IdentityDb — not the ConnectionStrings:mongodb that MongoConnectionResolver
    // actually reads. Profession resolves its client eagerly and crashed on startup; the other six
    // would have failed on their first request. The canonical key must be injected explicitly.
    [Theory]
    [InlineData("booking")]
    [InlineData("calendar")]
    [InlineData("customer")]
    [InlineData("identity")]
    [InlineData("profession")]
    [InlineData("provider")]
    [InlineData("services")]
    public async Task EveryServiceReceivesTheCanonicalMongoConnectionStringKey(string serviceName)
    {
        var resource = Resource(BuildModel(), serviceName);
        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run));

        foreach (var annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context);
        }

        Assert.Contains("ConnectionStrings__mongodb", context.EnvironmentVariables.Keys);
    }

    // ISSUE-001: with WithDataVolume(), an auto-generated MongoDB password is regenerated on every
    // run while the volume keeps the first one, so the health check fails from run two onward and
    // every service hangs in Waiting. The password must be a declared secret parameter — stable
    // across runs, and still masked in the dashboard.
    [Fact]
    public void MongoDbPasswordIsAStableSecretParameter()
    {
        var builder = BuildModel();

        var parameter = Assert.IsAssignableFrom<ParameterResource>(Resource(builder, "mongodb-password"));

        Assert.True(parameter.Secret, "mongodb-password must be declared secret.");

        var mongo = Assert.IsAssignableFrom<MongoDBServerResource>(Resource(builder, "mongodb"));

        Assert.Same(parameter, mongo.PasswordParameter);
    }

    // Only Identity signs tokens; every service validates them. Handing the private key to all
    // seven would widen the blast radius for no benefit.
    [Fact]
    public void OnlyIdentityReceivesThePrivateKey()
    {
        var builder = BuildModel();

        Assert.Contains("jwt-private-key", References(builder, "identity"));

        foreach (var name in ExpectedServices.Where(service => service != "identity"))
        {
            Assert.DoesNotContain("jwt-private-key", References(builder, name));
        }
    }

    [Fact]
    public void EveryServiceReceivesThePublicKey()
    {
        var builder = BuildModel();

        foreach (var name in ExpectedServices)
        {
            Assert.Contains("jwt-public-key", References(builder, name));
        }
    }

    // E-6: a service that starts before MongoDB accepts connections fails its first request.
    [Fact]
    public void EveryServiceWaitsForMongoDb()
    {
        var builder = BuildModel();

        foreach (var name in ExpectedServices)
        {
            Assert.Contains("mongodb", Waits(builder, name));
        }
    }

    // Kafka is only waited on where it is actually used.
    [Theory]
    [InlineData("booking")]
    [InlineData("customer")]
    [InlineData("provider")]
    public void KafkaConsumersWaitForKafka(string serviceName)
    {
        Assert.Contains("kafka", Waits(BuildModel(), serviceName));
    }

    // --- Cloud publish shape (docs/deployment.md) ------------------------------------------------

    // A dev container on a persistent volume is not a production database. Publishing must hand the
    // services connection strings to managed MongoDB and Kafka instead of provisioning containers.
    [Fact]
    public void CloudTargetProvisionsNoDataContainers()
    {
        var builder = BuildModel(DeploymentTarget.Cloud);

        Assert.Empty(builder.Resources.OfType<MongoDBServerResource>());
        Assert.Empty(builder.Resources.OfType<KafkaServerResource>());
        Assert.DoesNotContain("mongodb-password", builder.Resources.Select(resource => resource.Name));
    }

    // The resource names are identical in both shapes, so a service receives the same environment
    // variables locally and in the cloud — only what is behind them changes.
    [Theory]
    [InlineData("agenda-buddy")]
    [InlineData("IdentityDb")]
    [InlineData("kafka")]
    public void CloudTargetSuppliesTheDataServiceAsAConnectionString(string resourceName)
    {
        var resource = Resource(BuildModel(DeploymentTarget.Cloud), resourceName);

        Assert.IsAssignableFrom<IResourceWithConnectionString>(resource);
    }

    // Container Apps keeps ingress internal unless told otherwise, which would deploy a stack
    // nothing can reach. The mobile app calls all seven services directly.
    [Fact]
    public void CloudTargetExposesEveryServiceExternally()
    {
        var builder = BuildModel(DeploymentTarget.Cloud);

        foreach (var name in ExpectedServices)
        {
            var endpoints = Resource(builder, name).Annotations.OfType<EndpointAnnotation>().ToList();

            Assert.NotEmpty(endpoints);
            Assert.Contains(endpoints, endpoint => endpoint.IsExternal);
        }
    }

    // WaitFor observes a lifecycle. A connection string to a managed service has none, so gating on
    // it would either be ignored or hang the deployment.
    [Fact]
    public void CloudTargetWaitsForNothing()
    {
        var builder = BuildModel(DeploymentTarget.Cloud);

        foreach (var name in ExpectedServices)
        {
            Assert.Empty(Waits(builder, name));
        }
    }

    // AzureEnvironmentResource is annotated ASPIREAZURE001 (evaluation only). Suppressed narrowly
    // here rather than repo-wide: if the type is renamed or removed in a later Aspire, these two
    // tests should fail to compile and force a look at the deployment story.
#pragma warning disable ASPIREAZURE001

    // Azure Container Apps infrastructure is registered for the cloud shape. The ACA environment
    // resource itself is only materialised during a real publish, so what is observable here is the
    // Azure environment the publisher hangs it off — asserting more would be asserting a fiction.
    [Fact]
    public void CloudTargetWiresAzureInfrastructure()
    {
        Assert.Single(BuildModel(DeploymentTarget.Cloud).Resources.OfType<AzureEnvironmentResource>());
    }

    [Fact]
    public void LocalTargetWiresNoAzureInfrastructure()
    {
        Assert.Empty(BuildModel().Resources.OfType<AzureEnvironmentResource>());
    }

#pragma warning restore ASPIREAZURE001

    // AC-1.4 is not a local-only concern: a hardcoded 603x port would collide with Container Apps
    // ingress just as surely as it collides with a second local checkout.
    [Fact]
    public void CloudTargetBindsNoHardcodedHostPort()
    {
        var builder = BuildModel(DeploymentTarget.Cloud);

        foreach (var name in ExpectedServices)
        {
            Assert.All(Resource(builder, name).Annotations.OfType<EndpointAnnotation>(), endpoint =>
            {
                Assert.Null(endpoint.Port);
                Assert.Null(endpoint.TargetPort);
            });
        }
    }

    // The signing keys are secret parameters in both shapes — locally they come from user secrets,
    // on publish azd prompts and keeps them in Key Vault (threat T-003).
    [Theory]
    [InlineData("jwt-public-key")]
    [InlineData("jwt-private-key")]
    public void CloudTargetKeepsTheJwtKeysSecret(string parameterName)
    {
        var parameter = Assert.IsAssignableFrom<ParameterResource>(
            Resource(BuildModel(DeploymentTarget.Cloud), parameterName));

        Assert.True(parameter.Secret, $"{parameterName} must be declared secret when publishing.");
    }
}
