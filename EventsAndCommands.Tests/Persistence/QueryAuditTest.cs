using System.Text.Json;
using EventAndCommands.Persistence;

namespace EventsAndCommands.Tests.Persistence;

/// <summary>
/// Pins <see cref="QueryAudit"/> — F-016-T18, requirement 16, AC-17, and the payload half of AC-24
/// (threat <b>T-005</b>).
/// </summary>
/// <remarks>
/// <para>
/// Every query handler used to build its audit <c>Event</c> inline and set
/// <c>Data = JsonSerializer.Serialize(&lt;the entire result&gt;)</c>. There were <b>18</b> such call sites
/// across <b>9</b> handlers, and <c>GetProvidersQueryHandler.cs:23</c> serialised every provider with its
/// embedded appointment book and customer emails into an unbounded, unindexed, never-pruned collection on
/// every call.
/// </para>
/// <para>
/// Reducing 18 copies to one factory is what makes the security property enforceable: "the audit payload
/// contains no entity data" is now a claim about <b>one</b> method, tested here, rather than a claim about
/// 18 blocks that a future edit could quietly break.
/// </para>
/// <para>
/// <b>CONSTITUTION §3's audit mandate is preserved, not weakened.</b> "Every command result (success or
/// fail) is persisted to the EventStore — do not remove this pattern." Every handler still writes a
/// success or fail event carrying operation, status and timestamp. Only the payload shrinks.
/// </para>
/// <para>
/// ⚠️ <b>Count correction.</b> The PRD, <c>ARCHITECTURE.md</c> §5, the plan and this task's own body all
/// say "all <b>ten</b> query handlers", inherited from
/// <c>docs/pdlc/context/15-cqrs-and-messaging.md:161</c> — which states "10 queries, 10 handlers" directly
/// above a table listing <b>9</b>. There are 9 queries and 9 handlers, verified by grep. Recorded as
/// finding N-1 in the wave-6 standup MOM.
/// </para>
/// </remarks>
public class QueryAuditTest
{
    private const string QueryType = "GetProvidersQuery";

    [Fact]
    public void Success_RecordsOperationStatusAndTimestamp()
    {
        var before = DateTime.UtcNow;

        var audit = QueryAudit.Success(QueryType, resultCount: 3);

        Assert.Equal(QueryType, audit.Type);
        Assert.Equal("Success", audit.Status);
        Assert.NotEqual(default, audit.Id);
        Assert.InRange(audit.TimeStamp, before.AddSeconds(-5), DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public void Failure_RecordsTheSameOperationWithAFailedStatus()
    {
        var audit = QueryAudit.Failure(QueryType);

        Assert.Equal(QueryType, audit.Type);
        Assert.Equal("Failed", audit.Status);
    }

    [Fact]
    public void T005_TheDataPayloadCarriesOnlyAResultCount()
    {
        // The whole payload, pinned exactly. Anything added here later is a deliberate contract change
        // that has to come past this test, which is the point.
        var audit = QueryAudit.Success(QueryType, resultCount: 42);

        using var data = JsonDocument.Parse(audit.Data!);

        Assert.Equal(["resultCount"], data.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(42, data.RootElement.GetProperty("resultCount").GetInt32());
    }

    [Fact]
    public void T005_TheDataPayloadIsUnaffectedByHowMuchWasRead()
    {
        // The amplification property stated directly: audit size must not scale with result size. A
        // 10,000-provider read must not write a 10,000-provider document.
        var small = QueryAudit.Success(QueryType, resultCount: 1);
        var enormous = QueryAudit.Success(QueryType, resultCount: 10_000);

        Assert.Equal(small.Data!.Length, enormous.Data!.Length - 4);
        Assert.True(enormous.Data.Length < 40, $"payload grew unexpectedly: {enormous.Data}");
    }

    [Fact]
    public void Failure_CountsZeroRecords()
    {
        using var data = JsonDocument.Parse(QueryAudit.Failure(QueryType).Data!);

        Assert.Equal(0, data.RootElement.GetProperty("resultCount").GetInt32());
    }

    [Fact]
    public void QueryAudit_DoesNotSetTheActor()
    {
        // Attribution is stamped centrally by EventStore, which is the only component that knows about
        // the caller. A handler cannot see one -- see AuditActorTest and the ADR-027 amendment.
        Assert.Null(QueryAudit.Success(QueryType, 1).Actor);
    }
}
