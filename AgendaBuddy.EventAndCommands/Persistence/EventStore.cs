using AgendaBuddy.Library.Configuration;
using Microsoft.AspNetCore.Http;

namespace AgendaBuddy.EventAndCommands.Persistence;

public class EventStore : IEventStore
{
    private const int DefaultRetentionDays = 400;

    private readonly IMongoCollection<Event> _eventCollection;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly int _retentionDays;

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
    /// Supplies the calling principal so <see cref="Event.Actor"/> can be stamped centrally (ADR-027).
    /// Attribution is stamped <b>here</b> rather than in each handler because no handler has access to
    /// the caller: <c>ClaimsPrincipal</c> is dropped at the endpoint, and the query objects carry no
    /// such property. Threading an actor parameter down through every handler instead would touch far
    /// more files to carry one audit field, and could be half-done — miss one handler and that path
    /// silently loses attribution. Setting it where the record is written cannot be half-done.
    /// <para>
    /// Accepted cost: this couples the CQRS kernel to ASP.NET, which it was not before. If
    /// <c>EventAndCommands</c> ever needs to go HTTP-free again, the seam is a small
    /// <c>IAuditActorProvider</c> interface owned here and implemented in <c>Library.ServerAuth</c>.
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

        _retentionDays = int.TryParse(configuration["EventStore:RetentionDays"], out var configured)
            ? configured
            : DefaultRetentionDays;
    }

    /// <remarks>
    /// This is the resolution to the audit trail outliving an erasure request (F-024): the
    /// `appointments` collection and a provider's embedded copy are already cleaned up on
    /// cancellation, but every command handler's audit record still carries the full entity it
    /// acted on (by design — that IS the audit content for a write, see <see cref="QueryAudit"/>'s
    /// remarks on why query and command audits are treated differently). Redacting one record on
    /// request is not attempted — a selectively-edited audit trail is indistinguishable from a
    /// tampered one. A bounded retention window is: no audit record, and nothing it carries,
    /// survives longer than <see cref="_retentionDays"/> days.
    /// </remarks>
    public async Task EnsureIndexAsync()
    {
        var ttlKeys = Builders<Event>.IndexKeys.Ascending(e => e.TimeStamp);
        var ttlOptions = new CreateIndexOptions { ExpireAfter = TimeSpan.FromDays(_retentionDays) };
        await _eventCollection.Indexes.CreateOneAsync(new CreateIndexModel<Event>(ttlKeys, ttlOptions));

        var typeKeys = Builders<Event>.IndexKeys.Ascending(e => e.Type);
        await _eventCollection.Indexes.CreateOneAsync(new CreateIndexModel<Event>(typeKeys));
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
