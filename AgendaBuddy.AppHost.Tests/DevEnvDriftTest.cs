using System.Text.RegularExpressions;
using Xunit;

namespace AgendaBuddy.AppHost.Tests;

/// <summary>
/// The drift check is what makes "dev is running main" a measured fact rather than a belief about a path
/// filter. This holds the parts of it that fail silently.
/// </summary>
/// <remarks>
/// <para>
/// <c>deploy-dev</c> fires off the <c>deployable</c> allowlist, which can only be as complete as whoever
/// last edited it, fails in the silent direction, and cannot notice a deploy that failed after CI went
/// green or an environment somebody stopped by hand.
/// <c>dev-env-drift.yml</c> replaces that trust with a comparison: read the commit stamped on the
/// resource group, diff it against <c>main</c>, redeploy if anything material differs.
/// </para>
/// <para>
/// Two properties make it worth anything, and both are invisible when broken — which is why they are
/// tested here rather than assumed. It has to work off an <b>inert denylist</b> (unknown paths count as
/// deployable, so a new project deploys without anybody registering it anywhere), and it must not share a
/// concurrency group with the workflows it calls.
/// </para>
/// </remarks>
public class DevEnvDriftTest
{
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

    private static string Workflow(string name) =>
        File.ReadAllText(Path.Combine(RepoRoot(), ".github", "workflows", name));

    private static string Drift() => Workflow("dev-env-drift.yml");

    /// <summary>The <c>INERT</c> pattern the drift check excludes, verbatim.</summary>
    private static string InertPattern()
    {
        var line = Array.Find(Drift().Split('\n'),
            l => l.TrimStart().StartsWith("INERT=", StringComparison.Ordinal));

        Assert.NotNull(line);

        var value = line!.Trim()["INERT=".Length..].Trim('\'');
        Assert.False(value.Length == 0, "the drift check's INERT pattern is empty, so nothing is excluded.");

        return value;
    }

    // ── The environment records what it is running ─────────────────────────────────────────────────

    /// <summary>
    /// A successful deploy stamps the commit onto the resource group, and only after the smoke test.
    /// </summary>
    /// <remarks>
    /// Nothing could previously answer "which commit is dev running" — the nearest thing was inferring it
    /// from <c>deploy.yml</c>'s run history, which is how dev sat three days behind main and produced three
    /// bug reports against already-fixed behaviour. The ordering is the substance of the guarantee: the tag
    /// asserts "deployed <em>and serving</em>", so a deploy whose gateway never answered must not write it.
    /// </remarks>
    [Fact]
    public void ASuccessfulDeployStampsTheCommitAfterTheSmokeTest()
    {
        var lines = Workflow("deploy.yml").Split('\n');

        var stamp = Array.FindIndex(lines,
            l => l.TrimStart().StartsWith("- name: Stamp the deployed commit", StringComparison.Ordinal));
        Assert.True(stamp >= 0,
            "deploy.yml no longer stamps the deployed commit, so dev-env-drift.yml cannot tell what is "
            + "running and will redeploy on every single check.");

        var smoke = Array.FindIndex(lines,
            l => l.TrimStart().StartsWith("- name: Smoke test", StringComparison.Ordinal));
        Assert.True(smoke >= 0, "deploy.yml no longer smoke-tests the gateway.");

        Assert.True(smoke < stamp,
            "the commit is stamped BEFORE the smoke test, so a deploy that never served traffic would "
            + "still claim to be running that commit — and the drift check would believe it.");

        // Merged, not replaced: azd stamps azd-env-name on this same group.
        Assert.Contains("--operation merge", Workflow("deploy.yml"), StringComparison.Ordinal);
        Assert.Contains("deployedSha=", Workflow("deploy.yml"), StringComparison.Ordinal);
    }

    /// <summary>Terraform must not strip the tag on the next apply.</summary>
    /// <remarks>
    /// <c>terraform apply</c> runs on every deploy, including <c>provision: false</c>. If the resource group
    /// managed its own tags, each apply would delete the stamp and the drift check would read "unknown" and
    /// redeploy forever.
    /// </remarks>
    [Fact]
    public void TheResourceGroupIgnoresTagChangesSoTheStampSurvivesTerraform()
    {
        var main = File.ReadAllText(
            Path.Combine(RepoRoot(), "infra", "terraform", "environment", "main.tf"));

        var rg = main.IndexOf("resource \"azurerm_resource_group\" \"environment\"", StringComparison.Ordinal);
        Assert.True(rg >= 0, "the environment resource group is no longer declared under that name.");

        var block = main[rg..Math.Min(main.Length, rg + 800)];
        Assert.Contains("ignore_changes = [tags]", block, StringComparison.Ordinal);
    }

    // ── Unknown means deploy ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// No service the AppHost declares may be treated as inert.
    /// </summary>
    /// <remarks>
    /// This is the assertion that stops the drift check degrading into the thing it replaced. Its whole
    /// value is that unrecognised paths count as deployable; putting a service directory on the inert list
    /// would make the check agree with a broken allowlist that nothing needs deploying, while dev ran stale
    /// code — the original defect, now with a green backstop confirming it.
    /// </remarks>
    [Theory]
    [MemberData(nameof(AutoDeployPathFilterTest.DeployedProjectDirectories),
        MemberType = typeof(AutoDeployPathFilterTest))]
    public void NoDeployedProjectIsTreatedAsInert(string projectDirectory)
    {
        var inert = new Regex(InertPattern());

        Assert.False(inert.IsMatch($"{projectDirectory}/Program.cs"),
            $"{projectDirectory} matches the drift check's INERT pattern, so a change to a service the "
            + "AppHost deploys would be read as 'nothing material differs' and never deployed.");
    }

    /// <summary>
    /// The shared projects every service compiles in are not inert either.
    /// </summary>
    /// <remarks>
    /// These carry no <c>Projects.AgendaBuddy_*</c> symbol, so the theory above cannot reach them — and a
    /// change to <c>AgendaBuddy.Library</c> alters every one of the eight images.
    /// </remarks>
    [Theory]
    [InlineData("AgendaBuddy.Library")]
    [InlineData("AgendaBuddy.Library.ServerAuth")]
    [InlineData("AgendaBuddy.ServiceDefaults")]
    [InlineData("AgendaBuddy.EventAndCommands")]
    [InlineData("AgendaBuddy.Booking.Core")]
    [InlineData("AgendaBuddy.Customer.Domain")]
    public void NoSharedOrLayerProjectIsTreatedAsInert(string projectDirectory)
    {
        var inert = new Regex(InertPattern());

        Assert.False(inert.IsMatch($"{projectDirectory}/Anything.cs"),
            $"{projectDirectory} matches the drift check's INERT pattern, but it compiles into the "
            + "deployed images.");
    }

    /// <summary>
    /// A brand-new top-level path is deployable by default.
    /// </summary>
    /// <remarks>
    /// The property that distinguishes this from an allowlist: nobody has to register a new project for it
    /// to be deployed. If this ever fails, the denylist has grown broad enough to swallow unknown paths and
    /// the check is back to trusting a list.
    /// </remarks>
    [Theory]
    [InlineData("AgendaBuddy.Payments.Api/Program.cs")]
    [InlineData("SomeBrandNewThing/Whatever.cs")]
    [InlineData("Directory.Build.props")]
    [InlineData("azure.yaml")]
    [InlineData("global.json")]
    [InlineData("infra/terraform/environment/main.tf")]
    public void AnUnrecognisedPathCountsAsDeployable(string path)
    {
        var inert = new Regex(InertPattern());

        Assert.False(inert.IsMatch(path),
            $"'{path}' is treated as inert, so a change to it would never reach the deployed environment. "
            + "The drift check's default must be 'deploy', not 'ignore'.");
    }

    /// <summary>The paths that genuinely cannot change what a container serves are excluded.</summary>
    /// <remarks>
    /// Without these the check would redeploy on a CHANGELOG line, which is the cost the <c>deployable</c>
    /// filter exists to avoid and would make the hourly schedule expensive rather than free.
    /// </remarks>
    [Theory]
    [InlineData("docs/deployment.md")]
    [InlineData("CHANGELOG.md")]
    [InlineData("CLAUDE.md")]
    [InlineData("bruno/agenda-buddy/collection.bru")]
    [InlineData(".github/workflows/dotnet.yml")]
    [InlineData("scripts/run-ios.sh")]
    [InlineData("AgendaBuddy.MobileApp/App.xaml")]
    [InlineData("AgendaBuddy.MobileApp.Tests/Views/BrandHeaderPresenceTest.cs")]
    [InlineData("AgendaBuddy.IntegrationTests/GlobalUsings.cs")]
    [InlineData("AgendaBuddy.Library.Tests/AvatarCatalogTest.cs")]
    [InlineData("AgendaBuddy.AppHost.Tests/DevEnvDriftTest.cs")]
    [InlineData("docker-compose.override.yml")]
    public void ProvablyInertPathsAreExcluded(string path)
    {
        var inert = new Regex(InertPattern());

        Assert.True(inert.IsMatch(path),
            $"'{path}' is not excluded, so the hourly drift check would redeploy the environment for it.");
    }

    // ── It must not deadlock against the workflows it calls ────────────────────────────────────────

    /// <summary>
    /// The drift check, the redeploy sequence and the deploy must hold three distinct concurrency groups.
    /// </summary>
    /// <remarks>
    /// This workflow calls <c>dev-redeploy.yml</c>, which calls <c>deploy.yml</c>. A parent sharing a group
    /// with its own descendant can never release it — the child's job is never created, and GitHub resolves
    /// the impossible pending job by failing it with no log and no check run. That is precisely what made
    /// run 358 show every visible job green and the run red, and adding a third caller is exactly when it
    /// would recur.
    /// </remarks>
    [Fact]
    public void TheDriftCheckDoesNotShareAConcurrencyGroupWithWhatItCalls()
    {
        var groups = new[] { "dev-env-drift.yml", "dev-redeploy.yml", "deploy.yml" }
            .Select(ConcurrencyGroup)
            .ToArray();

        Assert.Equal(groups.Length, groups.Distinct(StringComparer.Ordinal).Count());
    }

    // A half-applied Terraform/azd run is worse than a queued one.
    [Fact]
    public void TheDriftCheckDoesNotCancelInProgress()
    {
        Assert.Contains("cancel-in-progress: false", Drift(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A workflow's concurrency group with every <c>${{ … }}</c> expression collapsed to a placeholder.
    /// </summary>
    /// <remarks>
    /// The normalisation is the point. Comparing the raw text reports
    /// <c>redeploy-${{ inputs.environment || 'dev' }}</c> and <c>redeploy-${{ inputs.environment }}</c> as
    /// different groups, when for <c>dev</c> they are the same string and deadlock exactly as if they had
    /// been written identically — so the un-normalised version of this test passed while the defect it
    /// exists to catch was present.
    /// </remarks>
    internal static string ConcurrencyGroup(string name)
    {
        var lines = Workflow(name).Split('\n');
        var start = Array.FindIndex(lines, l => l.StartsWith("concurrency:", StringComparison.Ordinal));
        Assert.True(start >= 0, $"{name} declares no workflow-level `concurrency:` block.");

        var group = Array.Find(lines[start..], l => l.TrimStart().StartsWith("group:", StringComparison.Ordinal));
        Assert.NotNull(group);

        return Regex.Replace(group!.Trim(), @"\$\{\{.*?\}\}", "<expr>");
    }

    // ── It has to actually run unattended ──────────────────────────────────────────────────────────

    /// <summary>
    /// The check is scheduled, not only dispatchable, and it heals on the schedule.
    /// </summary>
    /// <remarks>
    /// A backstop that only runs when somebody remembers to press it is not a backstop. <c>inputs</c> is
    /// empty on a <c>schedule</c> event, so the heal condition has to name <c>schedule</c> explicitly —
    /// relying on the <c>heal</c> input's default would evaluate to false and the scheduled run would
    /// report drift forever without correcting it.
    /// </remarks>
    [Fact]
    public void TheDriftCheckIsScheduledAndHealsOnASchedule()
    {
        var drift = Drift();

        Assert.Contains("schedule:", drift, StringComparison.Ordinal);
        Assert.Contains("cron:", drift, StringComparison.Ordinal);
        Assert.Contains("workflow_dispatch:", drift, StringComparison.Ordinal);

        var heal = Array.Find(drift.Split('\n'),
            l => l.Contains("needs.check.outputs.drift == 'true'", StringComparison.Ordinal)
                 && l.Contains("if:", StringComparison.Ordinal));

        Assert.NotNull(heal);
        Assert.Contains("github.event_name == 'schedule'", heal!, StringComparison.Ordinal);
    }

    /// <summary>
    /// A missing stamp is read as drift, not as "current".
    /// </summary>
    /// <remarks>
    /// The one place an optimistic default would be actively harmful: an environment that has never been
    /// stamped, or whose last deploy died before its smoke test, would be reported in sync and left running
    /// whatever it happened to have.
    /// </remarks>
    [Fact]
    public void AnAbsentStampIsTreatedAsDrift()
    {
        var lines = Drift().Split('\n');

        var guard = Array.FindIndex(lines, l => l.Contains("if [ -z \"$deployed\" ]", StringComparison.Ordinal));
        Assert.True(guard >= 0, "the drift check no longer handles an absent deployedSha tag.");

        // The next non-comment line has to call drift(), not sync().
        var next = Array.Find(lines[(guard + 1)..], l => l.Trim().Length > 0 && !l.TrimStart().StartsWith('#'));
        Assert.NotNull(next);
        Assert.Contains("drift ", next!, StringComparison.Ordinal);
    }

    /// <summary>
    /// The redeploy is delegated to <c>dev-redeploy.yml</c> rather than reimplemented.
    /// </summary>
    /// <remarks>
    /// CLAUDE.md records why: the standalone version of this sequence restated the deployable-path list in
    /// a second file, and a duplicate fails silently. Three callers, one implementation.
    /// </remarks>
    [Fact]
    public void HealingReusesTheRedeploySequence()
    {
        Assert.Contains("uses: ./.github/workflows/dev-redeploy.yml", Drift(), StringComparison.Ordinal);
    }
}
