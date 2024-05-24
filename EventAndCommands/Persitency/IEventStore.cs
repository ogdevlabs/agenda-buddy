namespace EventAndCommands.Persitency;

public interface IEventStore
{
    Task SaveAsync(Event @event);
    Task<IEnumerable<Event>> GetEventsAsync(ObjectId aggregateId);
}