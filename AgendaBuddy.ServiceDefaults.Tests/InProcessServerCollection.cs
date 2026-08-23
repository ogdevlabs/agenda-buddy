using Xunit;

namespace AgendaBuddy.ServiceDefaults.Tests;

/// <summary>
/// The collection every test class that starts an in-process server must join, so that no two of them run at
/// the same time.
/// </summary>
/// <remarks>
/// <para>
/// <b>OpenTelemetry's ASP.NET Core instrumentation is process-wide.</b> Two <c>TracerProvider</c>s alive at
/// once in one process do not reliably each receive every activity — the DiagnosticSource subscription and the
/// activity listeners are global — so a test that asserts on what its own in-memory exporter received can lose
/// spans to a provider belonging to another class. The symptom is exactly what it was here:
/// <c>ExportedSpan_IdentifiesTheEndpointByRouteTemplate</c> failing on roughly one run in three with its
/// expected span simply absent, and passing when run alone.
/// </para>
/// <para>
/// This assembly had one server-starting class for a long time, so the problem could not appear. F-014 added a
/// second (<c>TransportSecurityTest</c>, from F-021) and the overlap became real. The same mechanism F-016
/// used for the integration harness applies here: one collection, parallelism off.
/// </para>
/// <para>
/// ⚠️ <b>Add any new class that calls <c>WebApplication.CreateBuilder</c> to this collection.</b> Forgetting
/// costs a flaky test in an unrelated file, which is a bad afternoon.
/// </para>
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class InProcessServerCollection
{
    public const string Name = "in-process-server";
}
