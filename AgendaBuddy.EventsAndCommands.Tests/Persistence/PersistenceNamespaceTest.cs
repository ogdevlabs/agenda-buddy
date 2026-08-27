using System.Reflection;

namespace AgendaBuddy.EventsAndCommands.Tests.Persistence;

/// <summary>
/// Pins AC-1: the audit-persistence types live in <c>AgendaBuddy.EventAndCommands.Persistence</c>,
/// not the long-standing misspelling that preceded it (see <c>Misspelling</c> below — spelled out
/// only in that constant, so a tree-wide search for the typo finds exactly one deliberate hit).
/// </summary>
/// <remarks>
/// <para>
/// Deliberately written against <see cref="Assembly.GetType(string)"/> rather than a
/// <c>using AgendaBuddy.EventAndCommands.Persistence;</c> directive. A direct reference would not compile
/// until the rename landed, which makes the red phase a build break rather than a failing
/// assertion — and a build break proves nothing about behaviour. Resolving the namespace by name
/// lets this test compile against the pre-rename tree, fail on the assertion, and then pass.
/// </para>
/// <para>
/// It also stays useful afterwards: it fails if any type is moved back, and unlike a grep over the
/// source tree it inspects what the compiler actually produced.
/// </para>
/// <para>
/// The rename is behaviour-preserving by construction — the <c>events</c> collection name comes
/// from the <c>EventsCollection</c> configuration key, not the namespace, and nothing serialized
/// stores a CLR type name. The existing suite is the regression test for behaviour; this test pins
/// only the naming half of the criterion.
/// </para>
/// </remarks>
public class PersistenceNamespaceTest
{
    private const string CorrectNamespace = "AgendaBuddy.EventAndCommands.Persistence";
    private const string Misspelling = "Persitency";

    private static readonly Assembly EventAndCommandsAssembly = typeof(ConfigurationLoader).Assembly;

    [Theory]
    [InlineData("Event")]
    [InlineData("EventStore")]
    [InlineData("IEventStore")]
    public void AuditPersistenceType_ResolvesUnderTheCorrectlySpelledNamespace(string typeName)
    {
        var resolved = EventAndCommandsAssembly.GetType($"{CorrectNamespace}.{typeName}");

        Assert.NotNull(resolved);
        Assert.Equal(CorrectNamespace, resolved!.Namespace);
    }

    [Fact]
    public void NoTypeInTheEventAndCommandsAssembly_StillUsesTheMisspelledNamespace()
    {
        var offenders = EventAndCommandsAssembly
            .GetTypes()
            .Where(t => t.Namespace?.Contains(Misspelling, StringComparison.Ordinal) == true)
            .Select(t => t.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Empty(offenders);
    }
}
