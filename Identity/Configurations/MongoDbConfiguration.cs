namespace Identity.Configurations;

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
        => _client = new MongoDB.Driver.MongoClient(
            configuration.GetSection("MongoDbSettings")["ConnectionString"]!);

    /// <summary>Returns the client this configuration wraps.</summary>
    public MongoClient MongoClient() => _client;
}
