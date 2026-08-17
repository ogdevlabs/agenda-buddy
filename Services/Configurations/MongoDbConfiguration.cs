namespace Services.Configurations;

public class MongoDbConfiguration : IMongoDbConfiguration
{
    private readonly MongoDB.Driver.MongoClient _client;

    /// <summary>
    /// Injected path: takes the process-wide singleton client instead of opening a second
    /// connection pool.
    /// </summary>
    /// <param name="client">The shared client registered in <c>Program.cs</c>.</param>
    public MongoDbConfiguration(IMongoClient client)
        => _client = (MongoDB.Driver.MongoClient)client;

    /// <summary>
    /// Legacy path, body unchanged. Retained because existing tests construct this class with a
    /// mocked <see cref="IConfiguration"/>, and AC-5.2 forbids editing them. Deliberately not
    /// routed through <c>MongoConnectionResolver</c>: those mocks stub only
    /// <c>GetSection</c>, so the resolver's indexer lookups would return null and throw.
    /// </summary>
    /// <param name="configuration">Configuration carrying the legacy connection string.</param>
    public MongoDbConfiguration(IConfiguration configuration)
    {
        var connectionString = configuration.GetSection("MongoDB")["ConnectionString"];

        // Guarded rather than null-forgiving: AC-4.2 forbids constructing a MongoClient from a
        // possibly-null argument, and the original `!` here was exactly that. The message names
        // the key so the failure is actionable.
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No MongoDB connection string found at MongoDB:ConnectionString. " +
                "Run under AgendaBuddy.AppHost, or set ConnectionStrings__mongodb.");
        }

        _client = new MongoDB.Driver.MongoClient(connectionString);
    }

    /// <summary>Returns the client this configuration wraps.</summary>
    public MongoClient MongoClient() => _client;
}
