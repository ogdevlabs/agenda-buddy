namespace AgendaBuddy.AppHost;

/// <summary>
/// Builds the Agenda Buddy resource graph: MongoDB, Kafka, and the seven API services.
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
    /// <returns>The same builder, so callers can chain.</returns>
    internal static IDistributedApplicationBuilder Configure(IDistributedApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Signing keys as secret parameters: Aspire prompts once, stores them in user secrets, and
        // masks them in the dashboard. This is what lets AC-1.1 hold in a shell with no exported
        // environment variables, without committing an .env file (threat T-003).
        var jwtPublicKey = builder.AddParameter("jwt-public-key", secret: true);
        var jwtPrivateKey = builder.AddParameter("jwt-private-key", secret: true);

        // Persistent volume so seeded data survives a restart — which is exactly why the root
        // password has to be pinned. Aspire's default generates a fresh one on every run and
        // rewrites the user secret, while the volume keeps the root user created by the first run.
        // From the second run on, the container's credentials no longer match the ones in the
        // volume, the mongodb health check never reaches Healthy, and every service gated by
        // WaitFor(mongo) sits in Waiting forever with nothing logged (ISSUE-001). A declared
        // secret parameter is stable across runs and stays masked in the dashboard (T-003).
        var mongoPassword = builder.AddParameter("mongodb-password", secret: true);

        var mongo = builder.AddMongoDB("mongodb", password: mongoPassword)
            .WithDataVolume();

        // Two logical databases, matching what the services already expect: the six domain
        // services share agenda_buddy, Identity owns IdentityDb. The resource name is hyphenated
        // because ASPIRE006 rejects underscores; the second argument keeps the physical name.
        var agendaDb = mongo.AddDatabase("agenda-buddy", "agenda_buddy");
        var identityDb = mongo.AddDatabase("IdentityDb");

        // No data volume, deliberately (edge case E-10): CreateTopicIfNotExist treats an existing
        // topic as a failure, so persisting topics would break re-registration on every restart
        // after the first.
        var kafka = builder.AddKafka("kafka");

        AddApi<Projects.Identity>("identity", identityDb, needsPrivateKey: true);
        AddApi<Projects.Booking>("booking", agendaDb, needsKafka: true);
        AddApi<Projects.Customer>("customer", agendaDb, needsKafka: true);
        AddApi<Projects.Provider>("provider", agendaDb, needsKafka: true);
        AddApi<Projects.Calendar>("calendar", agendaDb);
        AddApi<Projects.Services>("services", agendaDb);
        AddApi<Projects.Profession>("profession", agendaDb);

        return builder;

        void AddApi<TProject>(
            string name,
            IResourceBuilder<IResourceWithConnectionString> database,
            bool needsKafka = false,
            bool needsPrivateKey = false)
            where TProject : IProjectMetadata, new()
        {
            // launchProfileName: null keeps Aspire from adopting the launch profile's
            // applicationUrl, which pins localhost:603x — the very thing the AppHost exists to
            // get rid of (AC-1.4).
            // WithReference alone injects ConnectionStrings__<resource name> — agenda-buddy or
            // IdentityDb — but MongoConnectionResolver's primary key is ConnectionStrings:mongodb,
            // which is what the services, their 28 resolution tests and the resolver's own error
            // message all name. So the reference is kept for the dashboard relationship and the
            // connection string is also injected under the canonical key, still pointing at the
            // service's own database.
            var service = builder.AddProject<TProject>(name, launchProfileName: null)
                .WithReference(database)
                .WithEnvironment("ConnectionStrings__mongodb", database)
                .WaitFor(mongo)
                .WithEnvironment("JWT_PUBLIC_KEY", jwtPublicKey);

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
            if (needsKafka) service.WithReference(kafka).WaitFor(kafka);
        }
    }
}
