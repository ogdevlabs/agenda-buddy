using AgendaBuddy.Library.Configuration;
using Microsoft.AspNetCore.Http;

namespace AgendaBuddy.EventAndCommands.Persistence;

public class EventStore : IEventStore
{
    private readonly IMongoCollection<Event> _eventCollection;
    private readonly IHttpContextAccessor _httpContextAccessor;

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
    /// <param name="httpContextAccessor">
    /// Supplies the calling principal so <see cref="Event.Actor"/> can be stamped centrally
    /// (F-016-T18 / ADR-027). Attribution is stamped <b>here</b> rather than in each handler because no
    /// handler has access to the caller: <c>ClaimsPrincipal</c> is dropped at the endpoint, the query
    /// objects carry no properties, and <c>RequestCollection</c> hand-constructs handlers from domain data.
    /// Threading an actor parameter down instead would have widened six public <c>IRequestCollection</c>
    /// interfaces and touched roughly thirty files to carry one audit field, and could be half-done — miss
    /// one handler and that path silently loses attribution. Setting it where the record is written cannot
    /// be half-done, and it attributes the eleven command handlers for free.
    /// <para>
    /// Accepted cost: this couples the CQRS kernel to ASP.NET, which it was not before. If F-019/F-020 ever
    /// needs <c>EventAndCommands</c> HTTP-free again, the seam is a small <c>IAuditActorProvider</c>
    /// interface owned here and implemented in <c>Library.ServerAuth</c>.
    /// </para>
    /// </param>
    public EventStore(
        IMongoClient client,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;

        var database = client.GetDatabase(
            MongoConnectionResolver.ResolveSetting(configuration, "DatabaseName", "agenda_buddy"));

        _eventCollection = database.GetCollection<Event>(
            MongoConnectionResolver.ResolveSetting(configuration, "EventsCollection", "events"));
    }

    /// <summary>
    /// Persists an audit event, stamping the calling principal onto it if it does not already carry one.
    /// </summary>
    /// <remarks>
    /// <c>??=</c> rather than <c>=</c>: a caller that has deliberately set an actor keeps it. Null is a
    /// correct outcome for an anonymous read or a hosted service — see <see cref="AuditActor"/>.
    /// </remarks>
    public async Task SaveAsync(Event @event)
    {
        @event.Actor ??= AuditActor.From(_httpContextAccessor.HttpContext?.User);

        await _eventCollection.InsertOneAsync(@event);
    }

    public async Task<IEnumerable<Event>> GetEventsAsync(ObjectId aggregateId)
    {
        var filter = Builders<Event>.Filter.Eq(e => e.Id, aggregateId);
        var events = await _eventCollection.Find(filter).ToListAsync();
        return events;
    }
}
