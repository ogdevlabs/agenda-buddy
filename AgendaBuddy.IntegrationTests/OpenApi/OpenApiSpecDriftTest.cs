using AgendaBuddy.IntegrationTests.Harness;

namespace AgendaBuddy.IntegrationTests.OpenApi;

/// <summary>
/// CI's spec-drift check: regenerate every service's "v1" OpenAPI
/// document with a byte-deterministic mechanism (<see cref="OpenApiSpecGenerator"/>, via
/// <see cref="OpenApiSpecCatalog"/>) and diff it against the committed baseline this repo tracks at
/// <c>docs/api/openapi/&lt;Service&gt;.json</c> (per ADR-048 — the drift
/// baseline is the spec body itself, not a previous CI run's artifact or a hash manifest, since
/// ADR-020's "do not commit" blocking condition cleared once
/// <c>GET /api/v1/providers</c> became authenticated and paginated).
/// </summary>
/// <remarks>
/// <para>
/// <b>No new CI wiring needed.</b> This class lives in <c>AgendaBuddy.IntegrationTests</c>, and
/// <c>.github/workflows/dotnet.yml</c>'s <c>integration</c> job's "Test (duration-enforced)" step
/// already runs <c>dotnet test AgendaBuddy.IntegrationTests/AgendaBuddy.IntegrationTests.csproj</c>
/// with no <c>--filter</c> — so this test runs there for free the moment it exists. Deliberately NOT
/// added as a new job or step: that would be incidental complexity with nothing to show for it.
/// </para>
/// <para>
/// <b>The actual red case</b> (a route changed without regenerating the spec) is proven by a
/// manual, reverted before/after run against a temporarily-mutated route in one service's
/// <c>Program.cs</c>, not encoded as a permanent test
/// here, since "a source file was edited and not regenerated" is a authoring-time condition, not a
/// runtime input this suite can supply itself without leaving the repo in a broken state between
/// test runs.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class OpenApiSpecDriftTest(CryptoSessionFixture crypto)
{
    // This proves the mirror
    // image of the drift-check's failure mode ("CI fails on the spec diff"): when nothing has drifted,
    // regenerating live must reproduce the committed bytes exactly, for all seven services.
    [Theory]
    [MemberData(nameof(AllServiceNames))]
    public void GivenAnOpenApiSpecIsCommittedForEachService_WhenItsServiceIsRegeneratedLive_ThenTheBytesMatchTheCommittedSpecExactly(string serviceName)
    {
        var regenerated = OpenApiSpecCatalog.Generators[serviceName](crypto.PublicKeyPem);
        var committedPath = Path.Combine(RepoRoot(), "docs", "api", "openapi", $"{serviceName}.json");

        Assert.True(
            File.Exists(committedPath),
            $"No committed baseline at {committedPath} — regenerate with " +
            "REGENERATE_OPENAPI_BASELINES=1 dotnet test AgendaBuddy.IntegrationTests " +
            "--filter FullyQualifiedName~OpenApiSpecBaselineWriter, and commit it (F-018-T16 / ADR-048). " +
            "NOT scripts/generate-openapi.sh — that reformats with python json.tool (4-space) and will " +
            "fail this check for every service.");

        var committed = File.ReadAllText(committedPath);

        if (regenerated == committed)
        {
            return;
        }

        Assert.Fail(BuildDriftMessage(serviceName, committedPath, committed, regenerated));
    }

    public static IEnumerable<object[]> AllServiceNames() =>
        OpenApiSpecCatalog.Generators.Keys.Select(name => new object[] { name });

    /// <summary>
    /// Names the drifted service and the first differing line, so a CI failure points at the
    /// route/operation that changed rather than dumping two ~15KB documents into the log.
    /// </summary>
    private static string BuildDriftMessage(string serviceName, string committedPath, string committed, string regenerated)
    {
        var committedLines = committed.Split('\n');
        var regeneratedLines = regenerated.Split('\n');
        var lineCount = Math.Max(committedLines.Length, regeneratedLines.Length);
        var firstDifference = Enumerable.Range(0, lineCount).First(i =>
            i >= committedLines.Length || i >= regeneratedLines.Length || committedLines[i] != regeneratedLines[i]);

        var committedLine = firstDifference < committedLines.Length ? committedLines[firstDifference].Trim() : "<end of file>";
        var regeneratedLine = firstDifference < regeneratedLines.Length ? regeneratedLines[firstDifference].Trim() : "<end of file>";

        return
            $"{serviceName}'s live OpenAPI spec no longer matches the committed baseline at " +
            $"{Path.GetRelativePath(RepoRoot(), committedPath)} (first differing line {firstDifference + 1}).\n" +
            $"  committed:   {committedLine}\n" +
            $"  regenerated: {regeneratedLine}\n" +
            "A route, verb, parameter, or operation ID changed without regenerating the spec — or the " +
            "baselines were rewritten by the wrong tool. Regenerate with:\n" +
            "  REGENERATE_OPENAPI_BASELINES=1 dotnet test AgendaBuddy.IntegrationTests " +
            "/p:MobileWorkloads=false --filter FullyQualifiedName~OpenApiSpecBaselineWriter\n" +
            "then commit the result, or revert the source change. Do NOT use " +
            "scripts/generate-openapi.sh for this: it reformats with python json.tool (4-space) while " +
            "these baselines are OpenApiJsonWriter output (2-space), so it fails this check for every " +
            "service at once.";
    }

    // Same repo-root-by-marker-file pattern as AgendaBuddy.AppHost.Tests/DockerAndComposeHygieneTest.cs
    // — duplicated rather than shared, since the two test projects don't reference each other and
    // this is a five-line helper, not a library worth a new dependency for.
    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null && !File.Exists(Path.Combine(current.FullName, "agenda-buddy.sln")))
        {
            current = current.Parent;
        }

        if (current is null)
        {
            throw new InvalidOperationException(
                $"Could not locate repo root (agenda-buddy.sln) walking up from {AppContext.BaseDirectory}.");
        }

        return current.FullName;
    }
}
