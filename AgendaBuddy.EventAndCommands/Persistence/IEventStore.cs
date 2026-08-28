namespace AgendaBuddy.EventAndCommands.Persistence;

public interface IEventStore
{
    Task SaveAsync(Event @event);
    Task<IEnumerable<Event>> GetEventsAsync(ObjectId aggregateId);

    /// <summary>
    /// Idempotent — safe to call at every service startup. Creates a TTL index on
    /// <see cref="Event.TimeStamp"/> so an audit record is deleted automatically once it is
    /// older than the configured retention window, and a secondary index on
    /// <see cref="Event.Type"/> for investigation queries.
    /// </summary>
    Task EnsureIndexAsync();
}
