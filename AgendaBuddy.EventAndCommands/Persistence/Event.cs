namespace AgendaBuddy.EventAndCommands.Persistence;

[ExcludeFromCodeCoverage]
public class Event
{
    [BsonElement("_id")] public ObjectId Id { get; set; }

    [BsonElement("timestamp")] public DateTime TimeStamp { get; set; }

    [BsonElement("status")] public string? Status { get; set; }

    [BsonElement("type")] public string? Type { get; set; }

    [BsonElement("data")] public string? Data { get; set; }

    /// <summary>
    /// The <c>sub</c> claim of the caller who caused this event, or <c>null</c> when there was no
    /// identifiable caller.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ADR-027. <c>15-cqrs-and-messaging.md:215</c>: <i>"No actor, no correlation, no request id. The
    /// audit trail cannot answer 'who did this'."</i>
    /// </para>
    /// <para>
    /// <b>Nullable and additive, so no backfill migration.</b> A backfill is not merely unnecessary but
    /// impossible: the actor of a historical anonymous read is genuinely unknown. Stamped centrally by
    /// <c>EventStore</c> — see <see cref="AuditActor"/>.
    /// </para>
    /// <para>
    /// ⚠️ Adding this field is not schema-change-free: a revert leaves harmless unread residue rather
    /// than no trace. Accepted knowingly.
    /// </para>
    /// </remarks>
    [BsonElement("actor")] public string? Actor { get; set; }
}
