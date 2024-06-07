using Mongo2Go;
using MongoDB.Driver;

namespace Services.Tests;

public class MongoTestBase : IAsyncLifetime
{
    private static MongoDbRunner _runner;
    protected IMongoClient MongoClient { get; private set; }
    protected IMongoDatabase Database { get; private set; }

    public Task InitializeAsync()
    {
        _runner = MongoDbRunner.Start();
        MongoClient = new MongoClient(_runner.ConnectionString);
        Database = MongoClient.GetDatabase("ab-events");

        // Optional: Clean the database before each test run
        Database.DropCollection("events");
        
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _runner.Dispose();
        return Task.CompletedTask;
    }
}