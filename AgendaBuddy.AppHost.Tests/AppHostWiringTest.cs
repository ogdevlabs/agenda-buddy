using AgendaBuddy.Library;
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

    /// <summary>
    /// The same graph, with parameter values supplied as command-line configuration.
    /// </summary>
    /// <remarks>
    /// Needed because the push parameters are declared only when a value is already present — so the
    /// configured and unconfigured graphs are genuinely different shapes, and both need covering.
    /// </remarks>
    private static IDistributedApplicationBuilder BuildModelWithPushCredentials(
        DeploymentTarget target = DeploymentTarget.Local)
    {
        var builder = DistributedApplication.CreateBuilder([
            "--Parameters:push-firebase-project-id=agendame-test",
            "--Parameters:push-service-account-json={\"type\":\"service_account\"}"
        ]);
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

    // Nothing in the graph produces or consumes messages, so no service may acquire a broker
    // reference. A broker used to be provisioned here for topics that were never read, and an
    // unreachable one made provider and customer creation fail -- i.e. it could only block signup.
    [Fact]
    public void LocalTargetReferencesNoMessageBroker() => AssertNoMessageBroker(DeploymentTarget.Local);

    [Fact]
    public void CloudTargetReferencesNoMessageBroker() => AssertNoMessageBroker(DeploymentTarget.Cloud);

    // DeploymentTarget is internal, so it cannot be an InlineData argument on a public Theory.
    private static void AssertNoMessageBroker(DeploymentTarget target)
    {
        var builder = BuildModel(target);

        Assert.DoesNotContain("kafka", builder.Resources.Select(resource => resource.Name));

        foreach (var name in ExpectedServices)
        {
            Assert.DoesNotContain("kafka", References(builder, name));
        }
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

    // The Gateway is deliberately exempt from the rule above — it is the one stable, client-facing
    // entry point, and a dynamic port here is what left AgendaBuddy.MobileApp addressing a dead
    // localhost port on any launch that did not go through scripts/run-ios.sh. Only the HOST port is
    // pinned; the port the Gateway itself listens on stays Aspire-assigned.
    [Fact]
    public void GatewayPinsItsHostPortLocally_SoClientsHaveOneStableAddress()
    {
        var endpoint = Assert.Single(
            Resource(BuildModel(DeploymentTarget.Local), "gateway").Annotations.OfType<EndpointAnnotation>());

        Assert.Equal(LocalGatewayAddress.Port, endpoint.Port);
        Assert.Null(endpoint.TargetPort);
    }

    // Pinning is a local-run affordance. In the Cloud shape the platform owns ingress
    // (WithExternalHttpEndpoints), so a fixed host port would be meaningless at best.
    [Fact]
    public void GatewayDoesNotPinAPortInTheCloudShape()
    {
        var endpoint = Assert.Single(
            Resource(BuildModel(DeploymentTarget.Cloud), "gateway").Annotations.OfType<EndpointAnnotation>());

        Assert.Null(endpoint.Port);
    }

    // The pinned port must stay clear of the 6030–6039 band a `Local (standalone)` run uses for the
    // seven services, or the Gateway could collide with — or be mistaken for — one of them.
    [Fact]
    public void GatewayPinnedPortSitsOutsideThePerServiceStandalonePortBand()
    {
        Assert.False(LocalGatewayAddress.Port is >= 6030 and <= 6039,
            $"the Gateway's pinned port {LocalGatewayAddress.Port} collides with the standalone service band.");
    }

    // The signing keys must never be committed. Declaring them secret keeps them in
    // user secrets and masked in the dashboard.
    [Theory]
    [InlineData("jwt-public-key")]
    [InlineData("jwt-private-key")]
    public void JwtKeyIsASecretParameter(string parameterName)
    {
        var parameter = Assert.IsAssignableFrom<ParameterResource>(Resource(BuildModel(), parameterName));

        Assert.True(parameter.Secret, $"{parameterName} must be declared secret.");
    }

    // The mail-provider key is a Cloud-only parameter, and that asymmetry is the point: an unset
    // secret parameter resolves to ValueMissing, which parks every resource referencing it in Waiting
    // with nothing logged (ISSUE-001). Declaring it locally would break the graph for any developer
    // without a Resend account, to enable a feature no local run needs.
    [Fact]
    public void LocalTargetDoesNotDeclareTheMailProviderKey()
    {
        Assert.DoesNotContain(
            "resend-api-key",
            BuildModel(DeploymentTarget.Local).Resources.Select(resource => resource.Name));
    }

    [Fact]
    public void CloudTargetDeclaresTheMailProviderKeyAsASecret()
    {
        var parameter = Assert.IsAssignableFrom<ParameterResource>(
            Resource(BuildModel(DeploymentTarget.Cloud), "resend-api-key"));

        Assert.True(parameter.Secret, "resend-api-key must be declared secret.");
    }

    // Identity is the only service that sends email, and only in the Cloud shape. Without this the
    // deployment has no working password reset -- the token has nowhere to go but a log, which is
    // exactly what it was doing before.
    [Fact]
    public async Task CloudTargetGivesIdentityTheMailProviderKey()
    {
        var variables = await PublishEnvironmentOf(BuildModel(DeploymentTarget.Cloud), "identity");

        Assert.Contains("Email__ApiKey", variables.Keys);
    }

    // ── Push delivery ───────────────────────────────────────────────────────────────────────────────
    // Push parameters are presence-gated rather than shape-gated, unlike resend-api-key: push has to be
    // testable against a local emulator, so it cannot be Cloud-only, but declaring it unconditionally would
    // resolve to ValueMissing on any machine without credentials and park every dependent service in Waiting
    // with nothing logged (ISSUE-001).

    [Theory]
    [InlineData("push-firebase-project-id")]
    [InlineData("push-service-account-json")]
    public void WithNoCredentialsConfigured_ThePushParametersAreNotDeclaredAtAll(string parameterName)
    {
        Assert.DoesNotContain(
            parameterName,
            BuildModel(DeploymentTarget.Local).Resources.Select(resource => resource.Name));
    }

    [Fact]
    public void WithCredentialsConfigured_TheServiceAccountJsonIsDeclaredSecret()
    {
        var builder = BuildModelWithPushCredentials();

        var credential = Assert.IsAssignableFrom<ParameterResource>(
            Resource(builder, "push-service-account-json"));
        Assert.True(credential.Secret, "push-service-account-json must be declared secret.");

        // The project id is not a secret -- it appears in the send URL and in google-services.json, which
        // ships inside every APK. Marking it secret would only mask it in the dashboard for no gain.
        var projectId = Assert.IsAssignableFrom<ParameterResource>(
            Resource(builder, "push-firebase-project-id"));
        Assert.False(projectId.Secret, "push-firebase-project-id is not a secret.");
    }

    /// <summary>
    /// Booking and Customer are the two services that produce notifications, so they are the two that need to
    /// reach FCM. Both variables or neither: a project id with no credential cannot mint a token, and a
    /// credential with no project id has no URL to send to.
    /// </summary>
    [Theory]
    [InlineData("booking")]
    [InlineData("customer")]
    public async Task WithCredentialsConfigured_TheNotificationProducersReceiveBothPushVariables(string service)
    {
        var variables = await EnvironmentOf(BuildModelWithPushCredentials(), service);

        Assert.Contains("Push__FirebaseProjectId", variables.Keys);
        Assert.Contains("Push__ServiceAccountJson", variables.Keys);
    }

    [Fact]
    public async Task NoServiceOtherThanTheNotificationProducersReceivesThePushCredential()
    {
        var builder = BuildModelWithPushCredentials();

        foreach (var name in ExpectedServices.Where(s => s is not ("booking" or "customer")))
        {
            Assert.DoesNotContain("Push__ServiceAccountJson", (await EnvironmentOf(builder, name)).Keys);
        }
    }

    // Without credentials nothing is injected, so AddPushDelivery resolves UnconfiguredPushSender.
    [Theory]
    [InlineData("booking")]
    [InlineData("customer")]
    public async Task WithNoCredentialsConfigured_NoPushVariablesAreInjected(string service)
    {
        var variables = await EnvironmentOf(BuildModel(DeploymentTarget.Local), service);

        Assert.DoesNotContain("Push__FirebaseProjectId", variables.Keys);
        Assert.DoesNotContain("Push__ServiceAccountJson", variables.Keys);
    }

    [Fact]
    public async Task NoServiceOtherThanIdentityReceivesTheMailProviderKey()
    {
        var builder = BuildModel(DeploymentTarget.Cloud);

        foreach (var name in ExpectedServices.Where(service => service != "identity"))
        {
            Assert.DoesNotContain("Email__ApiKey", (await PublishEnvironmentOf(builder, name)).Keys);
        }
    }

    /// <summary>
    /// Environment variables this resource would be PUBLISHED with. Distinct from the Run-mode helper
    /// below: resolving a secret parameter in Run mode materialises its value and throws when it has
    /// none, and the Cloud-only parameters deliberately have no local value.
    /// </summary>
    private static async Task<IDictionary<string, object>> PublishEnvironmentOf(
        IDistributedApplicationBuilder builder, string name)
    {
        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Publish));

        foreach (var annotation in Resource(builder, name).Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context);
        }

        return context.EnvironmentVariables;
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

    // ── The two configuration-gated security controls ─────────────────────────────────────
    //
    // These live here rather than in a service's own tests because the composition root is what decides
    // them. Gating on IsProduction() would have been wrong in a way no service-level test could show:
    // every service runs as PRODUCTION under this AppHost, so "production" does not mean "deployed"
    // here (ARCHITECTURE.md D-6).

    /// <summary>Environment variables this resource would be started with.</summary>
    private static async Task<Dictionary<string, object>> EnvironmentOf(
        IDistributedApplicationBuilder builder, string name)
    {
        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run));

        foreach (var annotation in Resource(builder, name).Annotations
                     .OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context);
        }

        return context.EnvironmentVariables;
    }

    // AC-14: a developer running the AppHost is not throttled and gets no HSTS header, and — the part
    // that needs the marker — the services know the flags are off deliberately, so they log no warning.
    [Theory]
    [InlineData("booking")]
    [InlineData("calendar")]
    [InlineData("customer")]
    [InlineData("identity")]
    [InlineData("profession")]
    [InlineData("provider")]
    [InlineData("services")]
    public async Task ALocalRunMarksItselfLocal_AndEnablesNeitherControl(string serviceName)
    {
        var environment = await EnvironmentOf(BuildModel(), serviceName);

        Assert.Equal("true", Assert.Contains("Security__Local", environment));
        Assert.DoesNotContain("Security__Hsts__Enabled", environment.Keys);
        Assert.DoesNotContain("Security__RateLimiting__Enabled", environment.Keys);
    }

    // The cloud graph turns both controls on, so shipping without
    // them requires editing this file rather than merely forgetting a key somewhere else. This is the
    // one test that distinguishes "the feature was written" from "the feature is switched on".
    [Theory]
    [InlineData("booking")]
    [InlineData("calendar")]
    [InlineData("customer")]
    [InlineData("identity")]
    [InlineData("profession")]
    [InlineData("provider")]
    [InlineData("services")]
    public async Task ACloudDeploymentEnablesHstsEverywhere(string serviceName)
    {
        var environment = await EnvironmentOf(BuildModel(DeploymentTarget.Cloud), serviceName);

        Assert.Equal("true", Assert.Contains("Security__Hsts__Enabled", environment));
        Assert.DoesNotContain("Security__Local", environment.Keys);
    }

    // The limiter goes only where BCrypt is spent (D-4). Enabling it on Calendar would throttle reads
    // that cost nothing, and would suggest the control is about traffic rather than about CPU.
    [Fact]
    public async Task ACloudDeploymentEnablesTheLimiterForIdentityOnly()
    {
        var builder = BuildModel(DeploymentTarget.Cloud);

        Assert.Equal(
            "true",
            Assert.Contains("Security__RateLimiting__Enabled", await EnvironmentOf(builder, "identity")));

        foreach (var name in ExpectedServices.Where(service => service != "identity"))
        {
            Assert.DoesNotContain(
                "Security__RateLimiting__Enabled", (await EnvironmentOf(builder, name)).Keys);
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

    // --- Cloud publish shape (docs/deployment.md) ------------------------------------------------

    // A dev container on a persistent volume is not a production database. Publishing must hand the
    // services a connection string to managed MongoDB instead of provisioning a container.
    [Fact]
    public void CloudTargetProvisionsNoDataContainers()
    {
        var builder = BuildModel(DeploymentTarget.Cloud);

        Assert.Empty(builder.Resources.OfType<MongoDBServerResource>());
        Assert.DoesNotContain("mongodb-password", builder.Resources.Select(resource => resource.Name));
    }

    // The resource names are identical in both shapes, so a service receives the same environment
    // variables locally and in the cloud — only what is behind them changes.
    [Theory]
    [InlineData("agenda-buddy")]
    [InlineData("IdentityDb")]
    public void CloudTargetSuppliesTheDataServiceAsAConnectionString(string resourceName)
    {
        var resource = Resource(BuildModel(DeploymentTarget.Cloud), resourceName);

        Assert.IsAssignableFrom<IResourceWithConnectionString>(resource);
    }

    // Container Apps keeps ingress internal unless told otherwise, which would deploy a stack
    // nothing can reach. The mobile app calls all seven services directly.
    [Fact]
    public void CloudTargetExposesOnlyTheGatewayExternally()
    {
        var builder = BuildModel(DeploymentTarget.Cloud);

        foreach (var name in ExpectedServices)
        {
            var endpoints = Resource(builder, name).Annotations.OfType<EndpointAnnotation>().ToList();

            Assert.NotEmpty(endpoints);
            Assert.DoesNotContain(endpoints, endpoint => endpoint.IsExternal);
        }

        var gatewayEndpoints = Resource(builder, "gateway").Annotations.OfType<EndpointAnnotation>().ToList();
        Assert.NotEmpty(gatewayEndpoints);
        Assert.Contains(gatewayEndpoints, endpoint => endpoint.IsExternal);
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
    // on publish azd prompts and keeps them in Key Vault.
    [Theory]
    [InlineData("jwt-public-key")]
    [InlineData("jwt-private-key")]
    public void CloudTargetKeepsTheJwtKeysSecret(string parameterName)
    {
        var parameter = Assert.IsAssignableFrom<ParameterResource>(
            Resource(BuildModel(DeploymentTarget.Cloud), parameterName));

        Assert.True(parameter.Secret, $"{parameterName} must be declared secret when publishing.");
    }

    // ── The eighth resource — the gateway ──────────────────────────────────────────────
    //
    // This is pure AppHost composition: the gateway resource exists and is wired to every one of the seven
    // services it could route to, mirroring how the file already wires dependents to mongodb.

    [Fact]
    public void GatewayIsRegistered()
    {
        var builder = BuildModel();

        Assert.IsAssignableFrom<ProjectResource>(Resource(builder, "gateway"));
    }

    // WithReference injects services__<name>__http__0 for each destination — the discovery keys
    // the routing config reads to resolve where to forward a request.
    [Theory]
    [InlineData("booking")]
    [InlineData("calendar")]
    [InlineData("customer")]
    [InlineData("identity")]
    [InlineData("profession")]
    [InlineData("provider")]
    [InlineData("services")]
    public void GatewayReferencesEveryService(string serviceName)
    {
        Assert.Contains(serviceName, References(BuildModel(), "gateway"));
    }

    // WaitFor on all seven means the gateway only reports healthy once every destination it could
    // route to is also healthy.
    [Theory]
    [InlineData("booking")]
    [InlineData("calendar")]
    [InlineData("customer")]
    [InlineData("identity")]
    [InlineData("profession")]
    [InlineData("provider")]
    [InlineData("services")]
    public void GatewayWaitsForEveryService(string serviceName)
    {
        Assert.Contains(serviceName, Waits(BuildModel(), "gateway"));
    }
}
