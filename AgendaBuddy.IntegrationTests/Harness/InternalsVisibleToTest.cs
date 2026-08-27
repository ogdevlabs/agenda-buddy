using System.Reflection;
using System.Runtime.CompilerServices;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// All seven services grant internals visibility to this project.
/// </summary>
/// <remarks>
/// <para>
/// Each service is resolved through <see cref="EntryPoints"/> — a distinct <b>public</b> type per
/// service — rather than through <c>Program</c>. All seven services use top-level statements, so each
/// emits an internal <c>Program</c> class in the <b>global namespace</b>; referencing all seven
/// assemblies from one test project therefore makes the bare name <c>Program</c> ambiguous. That is
/// resolvable with <c>extern alias</c>, but a public per-service type is simpler and needs no build
/// plumbing. <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/> only
/// uses <c>typeof(TEntryPoint).Assembly</c> to locate the entry point, so any type from the service
/// assembly works.
/// </para>
/// <para>
/// Worth stating plainly: that means <c>InternalsVisibleTo</c> is <em>not</em> what makes the harness
/// able to host a service — the original rationale for AC-2. It remains genuinely useful (asserting
/// on internal helpers, and future tier tests), and it is an approved acceptance
/// criterion, so it is implemented as specified. But the reason recorded in the PRD is no longer the
/// operative one, and that is worth knowing rather than discovering later.
/// </para>
/// </remarks>
public class InternalsVisibleToTest
{
    private const string ThisAssembly = "AgendaBuddy.IntegrationTests";

    public static TheoryData<string, Assembly> ServiceAssemblies()
    {
        var data = new TheoryData<string, Assembly>();
        foreach (var (name, assembly) in EntryPoints.All)
        {
            data.Add(name, assembly);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ServiceAssemblies))]
    public void Service_GrantsInternalsVisibilityToTheIntegrationTestProject(string serviceName, Assembly serviceAssembly)
    {
        var grantedTo = serviceAssembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(a => a.AssemblyName.Split(',')[0].Trim())
            .ToList();

        Assert.Contains(
            ThisAssembly,
            grantedTo,
            StringComparer.Ordinal);

        Assert.True(
            grantedTo.Contains(ThisAssembly, StringComparer.Ordinal),
            $"{serviceName} does not grant InternalsVisibleTo to {ThisAssembly}. " +
            $"It currently grants: {(grantedTo.Count == 0 ? "(nothing)" : string.Join(", ", grantedTo))}.");
    }

    [Fact]
    public void AllSevenServices_AreReferencedAndDistinct()
    {
        var assemblies = EntryPoints.All.Select(e => e.Assembly).ToList();

        Assert.Equal(7, assemblies.Count);
        Assert.Equal(7, assemblies.Select(a => a.GetName().Name).Distinct(StringComparer.Ordinal).Count());
    }
}
