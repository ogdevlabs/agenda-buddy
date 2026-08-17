namespace Profession.Extensions;

/// <summary>
/// Seeds the profession catalogue once, after the host has started.
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
    string collectionName) : IHostedService
{
    /// <summary>
    /// Inserts the seed data when the collection is empty. A seeding failure must not stop the
    /// service from starting — the API is useful without a pre-populated catalogue, and the
    /// readiness probe already reports an unreachable database.
    /// </summary>
    /// <param name="cancellationToken">Token that aborts startup.</param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var collection = client.GetDatabase(databaseName)
            .GetCollection<ProfessionEntity>(collectionName);

        if (!await collection.Find(_ => true).AnyAsync(cancellationToken))
        {
            await collection.InsertManyAsync(
                ProfessionSeedData.SeedData(), cancellationToken: cancellationToken);
        }
    }

    /// <summary>Nothing to unwind — seeding is a one-shot startup action.</summary>
    /// <param name="cancellationToken">Token that aborts shutdown.</param>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
