namespace AgendaBuddy.Profession.Extensions;

/// <summary>
/// Seeds the profession catalogue, after the host has started, and keeps it topped up.
/// </summary>
/// <remarks>
/// This work used to happen during service registration, blocking on <c>.Wait()</c>. Running it
/// as a hosted service keeps it genuinely asynchronous and lets it resolve the shared
/// <see cref="IMongoClient"/> from DI, which is not available while the container is still being
/// built.
/// </remarks>
/// <param name="client">The shared client.</param>
/// <param name="databaseName">Database holding the profession catalogue.</param>
/// <param name="collectionName">Collection holding the profession catalogue.</param>
public class ProfessionSeedHostedService(
    IMongoClient client,
    string databaseName,
    string collectionName,
    ILogger<ProfessionSeedHostedService> logger) : IHostedService
{
    /// <summary>
    /// Inserts any seed entries the collection is missing by name, on every startup.
    /// </summary>
    /// <param name="cancellationToken">Token that aborts startup.</param>
    /// <remarks>
    /// Previously this only ran when the whole collection was empty, so an already-seeded
    /// database (every deployed environment, after its first run) never picked up a profession
    /// added to <see cref="ProfessionSeedData"/> afterwards — the only way to add one was a manual
    /// write against that environment's database. Comparing by <c>Name</c> instead makes adding a
    /// profession to the seed list and deploying the normal way sufficient on its own, with no
    /// separate migration step, while still never re-inserting or duplicating an existing one.
    /// A seeding failure is logged and swallowed rather than thrown. An exception out of
    /// <c>StartAsync</c> aborts the host, which would mean an unreachable database prevents the
    /// service from starting at all — the opposite of AC-4.1, and worse than the behaviour this
    /// replaced. The API is still useful without a pre-populated catalogue, and the readiness
    /// probe already reports the database as unreachable.
    /// </remarks>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var collection = client.GetDatabase(databaseName)
                .GetCollection<ProfessionEntity>(collectionName);

            var existingNames = await collection.Find(_ => true)
                .Project(profession => profession.Name)
                .ToListAsync(cancellationToken);
            var existingNameSet = new HashSet<string>(existingNames, StringComparer.Ordinal);

            var missing = ProfessionSeedData.SeedData()
                .Where(profession => !existingNameSet.Contains(profession.Name))
                .ToList();

            if (missing.Count > 0)
                await collection.InsertManyAsync(missing, cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception,
                "Seeding the profession catalogue failed. The service will start anyway; " +
                "the readiness probe reports database availability.");
        }
    }

    /// <summary>Nothing to unwind — seeding is a one-shot startup action.</summary>
    /// <param name="cancellationToken">Token that aborts shutdown.</param>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
