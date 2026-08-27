using System.Xml.Linq;
using Xunit;

namespace AgendaBuddy.AppHost.Tests;

/// <summary>
/// AC-4: <c>dotnet publish -t:PublishContainer</c> must succeed for each of the seven services with
/// no NETSDK1152 file-conflict error. The conflict is entirely mechanical — <c>AgendaBuddy.EventAndCommands</c>'s
/// own <c>appsettings.json</c> was marked <c>CopyToOutputDirectory: Always</c>, which every consumer's
/// publish output inherits via the <c>ProjectReference</c>, colliding with that consumer's own file at
/// the same relative path — so a structural assertion on the MSBuild metadata is a faster and equally
/// precise guard than re-running a real publish per service in this Docker-free unit gate (ADR-031).
/// Whether the fix actually makes <c>dotnet publish -t:PublishContainer</c> succeed end-to-end is
/// verified live and recorded in this task's build notes — the same split <see cref="AppHostWiringTest"/>
/// uses for AC-1.1 ("verified manually in T-10").
/// </summary>
public class PublishContainerTest
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "agenda-buddy.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("Could not locate repo root from test base directory.");
    }

    private static XDocument LoadCsproj(string relativePath) =>
        XDocument.Load(Path.Combine(RepoRoot, relativePath));

    /// <summary>
    /// Finds every `&lt;None Update="appsettings*.json"&gt;` / `&lt;Content Update="appsettings*.json"&gt;`
    /// item in a project file that sets `CopyToOutputDirectory`. A project with no such item relies on
    /// the Web SDK's own default copy behavior for its own file, which never collides with anything.
    /// </summary>
    private static bool CopiesItsOwnAppSettingsToOutput(XDocument csproj) =>
        csproj.Descendants()
            .Where(element => element.Name.LocalName is "None" or "Content")
            .Where(element => (string?)element.Attribute("Update") is { } update &&
                               update.Contains("appsettings", StringComparison.OrdinalIgnoreCase))
            .Any(element => element.Element(element.Name.Namespace + "CopyToOutputDirectory") is not null);

    // AC-4 root cause: AgendaBuddy.EventAndCommands is a class library referenced by all seven services' publish
    // output. If IT also copies its own appsettings.json to its own output, that copy travels with it
    // into every consumer's publish folder and collides with the consumer's own file (NETSDK1152).
    [Fact]
    public void EventAndCommandsDoesNotCopyItsOwnAppSettingsToOutput()
    {
        Assert.False(
            CopiesItsOwnAppSettingsToOutput(LoadCsproj("AgendaBuddy.EventAndCommands/AgendaBuddy.EventAndCommands.csproj")),
            "AgendaBuddy.EventAndCommands.csproj must not copy its own appsettings.json to its output directory — " +
            "every one of the 7 services that reference it inherits the copy via ProjectReference, " +
            "colliding with that service's own appsettings.json at dotnet publish time (NETSDK1152).");
    }

    // Customer/Provider suppressed the symptom rather than the cause. Once the root fix lands, both
    // must publish clean without the suppression — restoring it would silently mask a regression here.
    [Theory]
    [InlineData("Customer/Customer.csproj")]
    [InlineData("Provider/Provider.csproj")]
    public void NoLongerSuppressesDuplicatePublishOutputFiles(string relativeCsprojPath)
    {
        var csproj = LoadCsproj(relativeCsprojPath);

        var suppressed = csproj.Descendants()
            .Where(element => element.Name.LocalName == "ErrorOnDuplicatePublishOutputFiles")
            .Select(element => element.Value)
            .Any(value => value.Equals("false", StringComparison.OrdinalIgnoreCase));

        Assert.False(
            suppressed,
            $"{relativeCsprojPath} still sets ErrorOnDuplicatePublishOutputFiles=false — that suppresses " +
            "the same NETSDK1152 collision AgendaBuddy.EventAndCommands' fix is meant to resolve at the root; once " +
            "the root fix lands this project must publish clean without the suppression.");
    }
}
