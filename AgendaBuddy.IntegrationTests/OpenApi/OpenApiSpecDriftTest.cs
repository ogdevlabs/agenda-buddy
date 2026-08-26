using AgendaBuddy.IntegrationTests.Harness;

namespace AgendaBuddy.IntegrationTests.OpenApi;

/// <summary>
/// F-018-T17, AC-19 / ADR-048. CI's spec-drift check: regenerate every service's "v1" OpenAPI
/// document with T16's byte-deterministic mechanism (<see cref="OpenApiSpecGenerator"/>, via
/// <see cref="OpenApiSpecCatalog"/>) and diff it against the committed baseline this repo tracks at
/// <c>docs/api/openapi/&lt;Service&gt;.json</c> (committed by F-018-T16 per ADR-048 — the drift
/// baseline is the spec body itself, not a previous CI run's artifact or a hash manifest, since
/// ADR-020's "do not commit" blocking condition cleared once F-016 authenticated/paginated
/// <c>GET /api/v1/providers</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>No new CI wiring needed.</b> This class lives in <c>AgendaBuddy.IntegrationTests</c>, and
/// <c>.github/workflows/dotnet.yml</c>'s <c>integration</c> job's "Test (duration-enforced)" step
/// already runs <c>dotnet test AgendaBuddy.IntegrationTests/AgendaBuddy.IntegrationTests.csproj</c>
/// with no <c>--filter</c> — so this test runs there for free the moment it exists, exactly the
/// option the task body flagged as simplest under YAGNI. Deliberately NOT added as a new job or
/// step: that would be incidental complexity with nothing to show for it, and it sidesteps any
/// line-level collision with F-018-T03 (concurrently editing <c>build-and-test</c>) or F-018-T15
/// (already merged into <c>integration</c>) — see the task's own coordination note.
/// </para>
/// <para>
/// <b>AC-19's actual red case</b> (a route changed without regenerating the spec) is proven by a
/// manual, reverted before/after run against a temporarily-mutated route in one service's
/// <c>Program.cs</c> — recorded in the task's completion report, not encoded as a permanent test
/// here, since "a source file was edited and not regenerated" is a authoring-time condition, not a
/// runtime input this suite can supply itself without leaving the repo in a broken state between
/// test runs.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class OpenApiSpecDriftTest(CryptoSessionFixture crypto)
{
    // F-018-US-06's Given: "an OpenAPI spec is committed for each service". This proves the mirror
    // image of AC-19's failure mode ("CI fails on the spec diff"): when nothing has drifted,
    // regenerating live must reproduce the committed bytes exactly, for all seven services.
    [Theory]
    [MemberData(nameof(AllServiceNames))]
    public void GivenAnOpenApiSpecIsCommittedForEachService_WhenItsServiceIsRegeneratedLive_ThenTheBytesMatchTheCommittedSpecExactly(string serviceName)
    {
        var regenerated = OpenApiSpecCatalog.Generators[serviceName](crypto.PublicKeyPem);
        var committedPath = Path.Combine(RepoRoot(), "docs", "api", "openapi", $"{serviceName}.json");

        Assert.True(
            File.Exists(committedPath),
            $"No committed baseline at {committedPath} — run ./scripts/generate-openapi.sh {serviceName} " +
            "and commit it (F-018-T16 / ADR-048).");

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
            "A route, verb, parameter, or operation ID changed without regenerating the spec. Run " +
            $"./scripts/generate-openapi.sh {serviceName} and commit the result, or revert the source change.";
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
