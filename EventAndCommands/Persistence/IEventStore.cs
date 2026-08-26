namespace EventAndCommands.Persistence;

public interface IEventStore
{
    Task SaveAsync(Event @event);
    Task<IEnumerable<Event>> GetEventsAsync(ObjectId aggregateId);
}
