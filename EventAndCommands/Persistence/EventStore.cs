using Library.Configuration;

namespace EventAndCommands.Persistence;

public class EventStore : IEventStore
{
    private readonly IMongoCollection<Event> _eventCollection;

    /// <summary>
    /// Resolves the events collection from the shared client.
    /// </summary>
    /// <param name="client">
    /// The process-wide singleton. This constructor previously built a <c>MongoClient</c> itself,
    /// and because every command and query handler writes an audit event while this type is
    /// registered per request scope, that meant a new client — with its own connection pool and
    /// monitoring threads — for every HTTP request. Injecting the shared client is AC-4.3.
    /// </param>
    /// <param name="configuration">Configuration supplying the database and collection names.</param>
    public EventStore(IMongoClient client, IConfiguration configuration)
    {
        var database = client.GetDatabase(
            MongoConnectionResolver.ResolveSetting(configuration, "DatabaseName", "agenda_buddy"));

        _eventCollection = database.GetCollection<Event>(
            MongoConnectionResolver.ResolveSetting(configuration, "EventsCollection", "events"));
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
