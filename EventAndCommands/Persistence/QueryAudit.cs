using System.Text.Json;

namespace EventAndCommands.Persistence;

/// <summary>
/// Builds the audit <see cref="Event"/> a query handler writes: operation, status, timestamp, and a
/// payload that never contains entity data.
/// </summary>
/// <remarks>
/// <para>
/// F-016-T18, requirement 16, AC-17, and the payload half of AC-24 (threat <b>T-005</b>).
/// </para>
/// <para>
/// <b>What this replaced.</b> Each of the 9 query handlers built its <see cref="Event"/> inline and set
/// <c>Data = JsonSerializer.Serialize(&lt;the whole result&gt;)</c> — 18 call sites in all.
/// <c>GetProvidersQueryHandler</c> serialised every provider, with its embedded appointment book and every
/// customer email, into the <c>events</c> collection on <em>every</em> call; that collection is unbounded,
/// unindexed and never pruned. <c>GetCustomersQuery</c> did the same for every customer record. A single
/// anonymous, unpaginated GET was therefore also a PII write amplifier.
/// </para>
/// <para>
/// <b>Why a factory rather than 18 edits.</b> "The audit payload contains no entity data" becomes a
/// property of <em>one</em> method that a test can pin, instead of 18 blocks that any future edit could
/// quietly reopen. The handlers keep their success/fail branches; only the construction moves.
/// </para>
/// <para>
/// <b>CONSTITUTION §3's audit mandate is preserved.</b> "Every command result (success or fail) is
/// persisted to the EventStore — do not remove this pattern." Every handler still writes an event on both
/// paths. Only the payload shrinks — from the dataset to its size.
/// </para>
/// <para>
/// <b>Command handlers are deliberately untouched</b> (11 of them). They serialise the entity the caller
/// just submitted, so it is not an amplification vector and it is the genuine audit content for a write.
/// </para>
/// <para>
/// <see cref="Event.Actor"/> is not set here. A handler has no access to the caller — see
/// <see cref="AuditActor"/> and <c>EventStore</c>, which stamps it centrally.
/// </para>
/// </remarks>
public static class QueryAudit
{
    /// <summary>The audit record for a query that returned data.</summary>
    /// <param name="queryType">The query name, recorded as <see cref="Event.Type"/>.</param>
    /// <param name="resultCount">
    /// How many records were read. The only payload field: it answers "how much was disclosed" for
    /// incident response and cannot itself contain personal data, however large the result was.
    /// </param>
    public static Event Success(string queryType, int resultCount) =>
        Create(queryType, "Success", resultCount);

    /// <summary>The audit record for a query that found nothing or failed.</summary>
    public static Event Failure(string queryType) => Create(queryType, "Failed", resultCount: 0);

    private static Event Create(string queryType, string status, int resultCount) => new()
    {
        Id = ObjectId.GenerateNewId(),
        TimeStamp = DateTime.UtcNow,
        Status = status,
        Type = queryType,

        // The size of the result, never the result. Audit volume must not scale with read volume.
        Data = JsonSerializer.Serialize(new { resultCount }),
    };
}
