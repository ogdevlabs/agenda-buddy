namespace AgendaBuddy.Profession.Extensions;

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
    string collectionName,
    ILogger<ProfessionSeedHostedService> logger) : IHostedService
{
    /// <summary>
    /// Inserts the seed data when the collection is empty.
    /// </summary>
    /// <param name="cancellationToken">Token that aborts startup.</param>
    /// <remarks>
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

            if (!await collection.Find(_ => true).AnyAsync(cancellationToken))
            {
                await collection.InsertManyAsync(
                    ProfessionSeedData.SeedData(), cancellationToken: cancellationToken);
            }
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
