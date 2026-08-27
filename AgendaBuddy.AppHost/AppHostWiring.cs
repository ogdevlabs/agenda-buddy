namespace AgendaBuddy.AppHost;

/// <summary>
/// Where the resource graph is being built for.
/// </summary>
internal enum DeploymentTarget
{
    /// <summary>
    /// Local development. MongoDB and Kafka run as containers the AppHost provisions itself.
    /// </summary>
    Local,

    /// <summary>
    /// Cloud publish. The data services are managed and arrive as connection strings, because a
    /// dev container on a persistent volume is not a production database.
    /// </summary>
    Cloud
}

/// <summary>
/// Builds the Agenda Buddy resource graph: the data services and the seven API services.
/// </summary>
/// <remarks>
/// Kept separate from <c>Program.cs</c> so the graph can be asserted without starting anything —
/// no container runtime is needed to check the wiring, only to run it.
/// </remarks>
internal static class AppHostWiring
{
    /// <summary>
    /// Adds every resource to <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The distributed application builder to populate.</param>
    /// <param name="target">
    /// Which shape to build. Defaults to <see cref="DeploymentTarget.Cloud"/> when the AppHost is
    /// publishing and <see cref="DeploymentTarget.Local"/> when it is running. Passed explicitly by
    /// tests, so both shapes are assertable without a publish run.
    /// </param>
    /// <returns>The same builder, so callers can chain.</returns>
    internal static IDistributedApplicationBuilder Configure(
        IDistributedApplicationBuilder builder,
        DeploymentTarget? target = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var deployTarget = target
            ?? (builder.ExecutionContext.IsPublishMode ? DeploymentTarget.Cloud : DeploymentTarget.Local);

        // Signing keys as secret parameters: Aspire prompts once, stores them in user secrets, and
        // masks them in the dashboard. This is what lets AC-1.1 hold in a shell with no exported
        // environment variables, without committing an .env file (threat T-003). On publish, azd
        // prompts for the same parameters and keeps them in Key Vault.
        var jwtPublicKey = builder.AddParameter("jwt-public-key", secret: true);
        var jwtPrivateKey = builder.AddParameter("jwt-private-key", secret: true);

        // Two logical databases, matching what the services already expect: the six domain services
        // share agenda_buddy, Identity owns IdentityDb. The resource name is hyphenated because
        // ASPIRE006 rejects underscores; the second argument keeps the physical name.
        IResourceBuilder<IResourceWithConnectionString> agendaDb;
        IResourceBuilder<IResourceWithConnectionString> identityDb;
        IResourceBuilder<IResourceWithConnectionString> kafka;

        // Only a resource the AppHost provisions itself can be waited on — a connection string to a
        // managed service has no lifecycle to observe, so E-6's gating is local-only.
        IResourceBuilder<IResource>? mongoToWaitFor = null;
        IResourceBuilder<IResource>? kafkaToWaitFor = null;

        if (deployTarget == DeploymentTarget.Local)
        {
            // Persistent volume so seeded data survives a restart — which is exactly why the root
            // password has to be pinned. Aspire's default generates a fresh one on every run and
            // rewrites the user secret, while the volume keeps the root user created by the first
            // run. From the second run on, the container's credentials no longer match the ones in
            // the volume, the mongodb health check never reaches Healthy, and every service gated
            // by WaitFor(mongo) sits in Waiting forever with nothing logged (ISSUE-001). A declared
            // secret parameter is stable across runs and stays masked in the dashboard (T-003).
            var mongoPassword = builder.AddParameter("mongodb-password", secret: true);

            var mongo = builder.AddMongoDB("mongodb", password: mongoPassword)
                .WithDataVolume();

            agendaDb = mongo.AddDatabase("agenda-buddy", "agenda_buddy");
            identityDb = mongo.AddDatabase("IdentityDb");

            // No data volume, deliberately (edge case E-10): CreateTopicIfNotExist treats an
            // existing topic as a failure, so persisting topics would break re-registration on
            // every restart after the first.
            var kafkaServer = builder.AddKafka("kafka");

            kafka = kafkaServer;
            mongoToWaitFor = mongo;
            kafkaToWaitFor = kafkaServer;
        }
        else
        {
            // Azure Container Apps is the deployment target Aspire supports first-class: `azd up`
            // turns this graph into an ACA environment with one container app per service. Naming
            // the environment here rather than letting azd infer it keeps the generated
            // infrastructure stable across deployments.
            builder.AddAzureContainerAppEnvironment("agenda-buddy-env");

            // The data services are external and managed: MongoDB Atlas and a hosted Kafka. Their
            // connection strings are supplied at deploy time, so nothing about production storage
            // is decided in this file. Names match the local resources, so the environment
            // variables the services receive are identical in both shapes.
            agendaDb = builder.AddConnectionString("agenda-buddy");
            identityDb = builder.AddConnectionString("IdentityDb");
            kafka = builder.AddConnectionString("kafka");
        }

        // spendsBcrypt: Identity's login and register are the only routes in the system that hash a
        // password — 262 ms of CPU each, measured — so it is the only service the per-IP limiter applies
        // to (threat T-101, ARCHITECTURE.md D-4).
        var identity = AddApi<Projects.AgendaBuddy_Identity>("identity", identityDb, needsPrivateKey: true, spendsBcrypt: true);
        var booking = AddApi<Projects.AgendaBuddy_Booking_Api>("booking", agendaDb, needsKafka: true);
        var customer = AddApi<Projects.Customer>("customer", agendaDb, needsKafka: true);
        var provider = AddApi<Projects.Provider>("provider", agendaDb, needsKafka: true);
        var calendar = AddApi<Projects.AgendaBuddy_Calendar_Api>("calendar", agendaDb);
        var services = AddApi<Projects.Services>("services", agendaDb);
        var profession = AddApi<Projects.AgendaBuddy_Profession_Api>("profession", agendaDb);

        // F-015-T05: the eighth resource. launchProfileName: null for the same reason as the seven
        // services (AC-1.4) — Gateway has no appsettings.json/launchSettings.json of its own yet, but
        // the AppHost must still assign its port rather than adopt one.
        //
        // WithReference injects services__<name>__http__0 for each destination — the service-discovery
        // keys F-015-T03's routing config reads to resolve where to forward a request (confirmed stable
        // across a destination's restart by F-015-T02's spike — Aspire's DCP orchestrator fronts every
        // WithReference-injected address with its own stable local proxy port, so it never goes stale).
        // WaitFor on all seven means the gateway only reports healthy once every destination it could
        // route to is also healthy, mirroring how every service above waits on mongodb/kafka before it
        // is considered up.
        var gateway = builder.AddProject<Projects.AgendaBuddy_Gateway>("gateway", launchProfileName: null);

        foreach (var service in new[] { booking, calendar, customer, provider, services, profession, identity })
        {
            gateway.WithReference(service);
            gateway.WaitFor(service);
        }

        return builder;

        IResourceBuilder<ProjectResource> AddApi<TProject>(
            string name,
            IResourceBuilder<IResourceWithConnectionString> database,
            bool needsKafka = false,
            bool needsPrivateKey = false,
            bool spendsBcrypt = false)
            where TProject : IProjectMetadata, new()
        {
            // launchProfileName: null keeps Aspire from adopting the launch profile's
            // applicationUrl, which pins localhost:603x — the very thing the AppHost exists to
            // get rid of (AC-1.4).
            //
            // WithReference alone injects ConnectionStrings__<resource name> — agenda-buddy or
            // IdentityDb — but MongoConnectionResolver's primary key is ConnectionStrings:mongodb,
            // which is what the services, their 28 resolution tests and the resolver's own error
            // message all name. So the reference is kept for the dashboard relationship and the
            // connection string is also injected under the canonical key, still pointing at the
            // service's own database.
            var service = builder.AddProject<TProject>(name, launchProfileName: null)
                .WithReference(database)
                .WithEnvironment("ConnectionStrings__mongodb", database)
                .WithEnvironment("JWT_PUBLIC_KEY", jwtPublicKey);

            // F-021's two configuration-gated controls. Gating them on configuration rather than on
            // IsProduction() is not a preference: services run as PRODUCTION under this AppHost, because
            // AddProject is called with launchProfileName: null while launchSettings.json sets
            // DOTNET_ENVIRONMENT=Development for the AppHost process alone. An environment-gated HSTS
            // would therefore emit Strict-Transport-Security for localhost — which browsers cache
            // stickily and across projects — and an environment-gated limiter would throttle every local
            // run (ARCHITECTURE.md D-6).
            //
            // So the composition root states which it is, and the services stop guessing:
            if (deployTarget == DeploymentTarget.Local)
            {
                // Both controls stay off, and the service knows that is deliberate rather than a
                // forgotten deployment key, so it logs no startup warning (D-7).
                service.WithEnvironment("Security__Local", "true");
            }
            else
            {
                // Threat T-103: the cloud graph turns them ON here, so shipping without them takes an
                // edit to this file rather than an omission somewhere else. HSTS everywhere; the limiter
                // only where BCrypt is spent.
                service.WithEnvironment("Security__Hsts__Enabled", "true");

                if (spendsBcrypt) service.WithEnvironment("Security__RateLimiting__Enabled", "true");
            }

            // That alone is not enough. Aspire also adopts each service's Kestrel:Endpoints from
            // appsettings.json, which pins the same 603x/703x ports by another route. Those keys
            // are deliberately retained for standalone and Compose runs (E-12), so the port is
            // cleared here in the orchestration graph rather than deleted from the services.
            // Clearing TargetPort too, since for a project resource the process binds it directly.
            foreach (var endpoint in service.Resource.Annotations.OfType<EndpointAnnotation>())
            {
                endpoint.Port = null;
                endpoint.TargetPort = null;
            }

            // Only Identity signs tokens; the rest merely validate them, so only Identity needs
            // the private key.
            if (needsPrivateKey) service.WithEnvironment("JWT_PRIVATE_KEY", jwtPrivateKey);

            // Only the three services that produce per-provider topics (A-3).
            if (needsKafka) service.WithReference(kafka);

            if (mongoToWaitFor is not null) service.WaitFor(mongoToWaitFor);
            if (needsKafka && kafkaToWaitFor is not null) service.WaitFor(kafkaToWaitFor);

            // The mobile app calls every service directly, so each one needs ingress. Container
            // Apps keeps them internal unless told otherwise, which would deploy a stack nothing
            // can reach. See docs/deployment.md on fronting these with a gateway before this is
            // anything more than a staging deployment.
            if (deployTarget == DeploymentTarget.Cloud) service.WithExternalHttpEndpoints();

            return service;
        }
    }
}
