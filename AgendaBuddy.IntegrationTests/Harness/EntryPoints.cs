using System.Reflection;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// The seven service entry-point assemblies, each resolved through a distinct <b>public</b> type.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why not <c>Program</c>.</b> Every service uses top-level statements, so each emits an internal
/// <c>Program</c> class in the <b>global namespace</b>. A single test project referencing all seven
/// assemblies therefore cannot write <c>WebApplicationFactory&lt;Program&gt;</c> — the name is
/// ambiguous across seven candidates. The textbook fix is <c>extern alias</c> per
/// <c>ProjectReference</c>, which means alias metadata in the csproj and <c>extern alias X;</c> at the
/// top of every consuming file.
/// </para>
/// <para>
/// <b>What we do instead.</b> <c>WebApplicationFactory&lt;TEntryPoint&gt;</c> only uses
/// <c>typeof(TEntryPoint).Assembly</c> to find the entry point, so <em>any</em> type from the service
/// assembly serves. Each service happens to expose a public, namespaced <c>MongoDbConfiguration</c>,
/// which makes the reference unambiguous with no build plumbing at all.
/// </para>
/// <para>
/// ⚠️ <b>Note the naming inconsistency this exposed.</b> Booking's namespace is
/// <c>AgendaBuddy.Booking.Configuration</c> (singular) while the other five domain services use
/// <c>*.Configurations</c> (plural). Pre-existing; not corrected here because renaming a namespace in
/// six services is not this task's scope and would bury the harness change. Recorded so the asymmetry
/// below reads as a fact about the codebase rather than a typo in this file.
/// </para>
/// <para>
/// The anchor types are an implementation detail of how the assembly is located — nothing here depends
/// on <c>MongoDbConfiguration</c>'s behaviour. If one is ever deleted, replace it with any other public
/// type from the same service.
/// </para>
/// </remarks>
internal static class EntryPoints
{
    /// <summary>Booking service entry-point assembly.</summary>
    public static Assembly Booking => typeof(global::AgendaBuddy.Booking.Configuration.MongoDbConfiguration).Assembly;

    /// <summary>Calendar service entry-point assembly.</summary>
    public static Assembly Calendar => typeof(global::AgendaBuddy.Calendar.Configurations.MongoDbConfiguration).Assembly;

    /// <summary>Customer service entry-point assembly.</summary>
    public static Assembly Customer => typeof(global::Customer.Configurations.MongoDbConfiguration).Assembly;

    /// <summary>Provider service entry-point assembly.</summary>
    public static Assembly Provider => typeof(global::Provider.Configurations.MongoDbConfiguration).Assembly;

    /// <summary>Services (provider service-catalogue) entry-point assembly.</summary>
    public static Assembly Services => typeof(global::Services.Configurations.MongoDbConfiguration).Assembly;

    /// <summary>Profession service entry-point assembly.</summary>
    public static Assembly Profession => typeof(global::AgendaBuddy.Profession.Configurations.MongoDbConfiguration).Assembly;

    /// <summary>Identity service entry-point assembly.</summary>
    public static Assembly Identity => typeof(global::AgendaBuddy.Identity.Configurations.MongoDbConfiguration).Assembly;

    /// <summary>All seven, paired with a display name for test output.</summary>
    public static IReadOnlyList<(string Name, Assembly Assembly)> All =>
    [
        (nameof(Booking), Booking),
        (nameof(Calendar), Calendar),
        (nameof(Customer), Customer),
        (nameof(Provider), Provider),
        (nameof(Services), Services),
        (nameof(Profession), Profession),
        (nameof(Identity), Identity),
    ];
}
