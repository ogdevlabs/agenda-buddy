using MongoDB.Driver;

namespace EventAndCommands.Persitency;

public class EventStore : IEventStore
{
    private readonly IMongoCollection<Event> _eventCollection;

    public EventStore()
    {
        var librarySettings = ConfigurationLoader.LoadConfiguration() ?? throw new ArgumentException(nameof(LibrarySettings));
        var client = new MongoClient(librarySettings.MongoDbSettings.ConnectionString);
        var database = client.GetDatabase(librarySettings.MongoDbSettings.DatabaseName);
        _eventCollection = database.GetCollection<Event>(librarySettings.MongoDbSettings.CollectionName);
    }
    
    public async Task SaveAsync(Event @event)
    {
        await _eventCollection.InsertOneAsync(@event);
    }

    public async Task<IEnumerable<Event>> GetEventsAsync(ObjectId aggregateId)
    {
        var filter = Builders<Event>.Filter.Eq(e => e.Id, aggregateId);
        var events = await _eventCollection.Find(filter).ToListAsync();
        return events;
    }
}