using System.Text.RegularExpressions;
using Xunit;

namespace AgendaBuddy.AppHost.Tests;

/// <summary>
/// The auto-deploy workflow decides whether a merge to main touched a deployed backend service by
/// prefix-matching a hand-maintained path list. This holds that list against the AppHost's own resource
/// graph.
/// </summary>
/// <remarks>
/// <para>
/// <b>The failure this exists for is silent in the worst direction.</b> Rename or add a service and
/// forget the list, and merges that change it stop triggering a deploy — no error, no red check, just a
/// dev environment that quietly drifts behind main until somebody notices the behaviour they shipped is
/// not there. CLAUDE.md already records that every path filter in <c>dotnet.yml</c>'s <c>changes</c> job
/// had to be updated for each of F-020's 12 project renames; this is the same trap with a quieter
/// symptom.
/// </para>
/// <para>
/// Structural, YAML-as-text, no CI run needed — the same pattern as
/// <see cref="SecurityScanAndDockerJobShapeTest"/> and <see cref="DockerAndComposeHygieneTest"/>.
/// </para>
/// </remarks>
public class AutoDeployPathFilterTest
{
    private const string WorkflowName = "main-deploy-dev.yml";

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

    private static string Workflow() =>
        File.ReadAllText(Path.Combine(RepoRoot(), ".github", "workflows", WorkflowName));

    /// <summary>
    /// Every project the AppHost declares as a resource — the seven services plus the Gateway — derived
    /// from <c>Projects.AgendaBuddy_*</c> in the app model rather than from a second hardcoded list, so
    /// adding a service to the graph is what makes this test demand a filter entry.
    /// </summary>
    public static TheoryData<string> DeployedProjectDirectories()
    {
        var appHost = Directory
            .EnumerateFiles(Path.Combine(RepoRoot(), "AgendaBuddy.AppHost"), "*.cs", SearchOption.TopDirectoryOnly)
            .Select(File.ReadAllText);

        var directories = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var source in appHost)
        {
            foreach (var match in Regex.Matches(source, @"Projects\.(AgendaBuddy_[A-Za-z0-9_]+)").Cast<Match>())
            {
                // Aspire derives the generated type name from the .csproj file name with dots replaced by
                // underscores, so the inverse recovers the project directory.
                directories.Add(match.Groups[1].Value.Replace('_', '.'));
            }
        }

        var data = new TheoryData<string>();
        foreach (var directory in directories)
            data.Add(directory);
        return data;
    }

    [Theory]
    [MemberData(nameof(DeployedProjectDirectories))]
    public void EveryAppHostDeclaredServiceAppearsInTheAutoDeployPathList(string projectDirectory)
    {
        Assert.Contains($"{projectDirectory}/", Workflow(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Sanity check on the discovery above: if the regex ever stops matching, every
    /// <see cref="EveryAppHostDeclaredServiceAppearsInTheAutoDeployPathList"/> case would vanish and the
    /// suite would go green having asserted nothing at all.
    /// </summary>
    [Fact]
    public void TheAppHostGraphYieldsTheEightDeployedProjects()
    {
        Assert.Equal(8, DeployedProjectDirectories().Count());
    }

    /// <summary>
    /// The shared projects every service compiles into. A change to <c>AgendaBuddy.Library</c> changes
    /// all seven services' behaviour, so it has to trigger a deploy even though no service directory was
    /// touched — this is exactly the class of omission that made a JWT-validation change run zero CI jobs
    /// before <c>AgendaBuddy.Library.ServerAuth</c> was added to <c>dotnet.yml</c>'s filters.
    /// </summary>
    [Theory]
    [InlineData("AgendaBuddy.Library/")]
    [InlineData("AgendaBuddy.Library.ServerAuth/")]
    [InlineData("AgendaBuddy.EventAndCommands/")]
    [InlineData("AgendaBuddy.ServiceDefaults/")]
    [InlineData("Directory.Build.props")]
    [InlineData("azure.yaml")]
    [InlineData("infra/terraform/")]
    public void TheSharedAndInfrastructurePathsAreCovered(string path)
    {
        Assert.Contains(path, Workflow(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Test projects and the mobile client must NOT trigger a deploy: neither changes any deployed
    /// behaviour, and the mobile client ships through TestFlight rather than azd.
    /// </summary>
    [Theory]
    [InlineData("AgendaBuddy.Library.Tests/")]
    [InlineData("AgendaBuddy.Booking.Tests/")]
    [InlineData("AgendaBuddy.IntegrationTests/")]
    [InlineData("AgendaBuddy.MobileApp/")]
    [InlineData("AgendaBuddy.MobileApp.Tests/")]
    public void NonDeployablePathsAreNotInTheList(string path)
    {
        // The path list is the only place a bare `<Project>/` prefix appears; the surrounding prose names
        // some of these deliberately ("Test projects are deliberately absent"), so the assertion is scoped
        // to the shell variable that actually drives the matching.
        var list = PathListBlock();

        Assert.DoesNotContain(path, list, StringComparison.Ordinal);
    }

    /// <summary>
    /// The <c>paths='…'</c> heredoc the workflow prefix-matches against, isolated from the surrounding
    /// comments so a path merely *mentioned* in prose cannot satisfy or break the assertions above.
    /// </summary>
    private static string PathListBlock()
    {
        var workflow = Workflow();
        var start = workflow.IndexOf("paths='", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{WorkflowName} no longer contains a `paths='` list; this test cannot see what it matches.");

        var end = workflow.IndexOf('\'', start + "paths='".Length);
        Assert.True(end > start, $"{WorkflowName}'s `paths='` list is unterminated.");

        return workflow[start..end];
    }

    // ── The guardrails themselves ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Off unless <c>AUTO_DEPLOY_DEV</c> is set. deploy.yml is manual "deliberately" until
    /// docs/deployment.md's "Before this is production" list is done, and item 1 on that list — rotating
    /// the Atlas credential (<c>agenda-buddy-41s</c>) — is still open. Merging this workflow must not
    /// start deploying on its own.
    /// </summary>
    [Fact]
    public void TheAutoDeployIsGatedOnAnExplicitOptInVariable()
    {
        Assert.Contains("vars.AUTO_DEPLOY_DEV", Workflow(), StringComparison.Ordinal);
    }

    /// <summary>
    /// It hangs off ".NET CI" completing rather than off the push, so main is never deployed before main
    /// itself is green — and it checks the conclusion, because <c>workflow_run</c>/<c>completed</c> fires
    /// on failure too.
    /// </summary>
    [Fact]
    public void ItDeploysOnlyAfterCiSucceededOnMain()
    {
        var workflow = Workflow();

        Assert.Contains("workflow_run:", workflow, StringComparison.Ordinal);
        Assert.Contains("workflows: [\".NET CI\"]", workflow, StringComparison.Ordinal);
        Assert.Contains("branches: [main]", workflow, StringComparison.Ordinal);
        Assert.Contains("workflow_run.conclusion", workflow, StringComparison.Ordinal);

        // Scoped to the trigger block, not the file: the surrounding comments discuss `push: branches:
        // [main]` at length as the thing this deliberately is NOT, so a whole-file search finds the prose
        // and asserts the opposite of what it means.
        Assert.DoesNotContain("push:", TriggerBlock(), StringComparison.Ordinal);
    }

    /// <summary>The workflow's <c>on:</c> block alone — the triggers, with none of the prose around them.</summary>
    private static string TriggerBlock()
    {
        var lines = Workflow().Split('\n');
        var start = Array.FindIndex(lines, l => l.StartsWith("on:", StringComparison.Ordinal));
        Assert.True(start >= 0, $"{WorkflowName} has no top-level `on:` block.");

        // Runs to the next top-level key — a line starting in column zero that is not a comment.
        var end = Array.FindIndex(lines, start + 1, l =>
            l.Length > 0 && !char.IsWhiteSpace(l[0]) && !l.StartsWith("#", StringComparison.Ordinal));

        return string.Join('\n', lines[start..(end < 0 ? lines.Length : end)]);
    }

    /// <summary>
    /// Never <c>cancel-in-progress</c>, and in deploy.yml's own concurrency group: a half-applied
    /// Terraform/azd run is worse than a queued one, and a manual dispatch must not race an automatic run.
    /// </summary>
    [Fact]
    public void ConcurrencyIsSharedWithTheManualDeployAndNeverCancels()
    {
        var workflow = Workflow();

        Assert.Contains("group: deploy-dev", workflow, StringComparison.Ordinal);
        Assert.Contains("cancel-in-progress: false", workflow, StringComparison.Ordinal);
    }

    /// <summary>
    /// The environment is restored after deploying.
    /// </summary>
    /// <remarks>
    /// <c>dev-env-stop</c> sets <c>minReplicas=0</c>, and <c>azd deploy</c> does not reset the scale rule
    /// — only <c>azd provision</c> re-applies it, and provision is deliberately <c>false</c> here. So
    /// without a restore step a stop→deploy leaves the new code sitting at zero replicas until the next
    /// scheduled 09:00 start, which is the whole reason this job has three stages rather than two.
    /// </remarks>
    [Fact]
    public void TheEnvironmentIsReturnedToItsScheduledStateAfterDeploying()
    {
        var workflow = Workflow();

        Assert.Contains("action: start", workflow, StringComparison.Ordinal);
        Assert.Contains("start_after", workflow, StringComparison.Ordinal);
        // provision must stay false on an application-code deploy, or every merge re-applies infrastructure.
        Assert.Contains("provision: false", workflow, StringComparison.Ordinal);
    }

    /// <summary>
    /// It reuses the existing reusable workflows rather than restating Terraform/azd wiring, so there is
    /// one implementation of "deploy" and one of "power" in this repository.
    /// </summary>
    [Fact]
    public void ItReusesTheExistingDeployAndPowerWorkflows()
    {
        var workflow = Workflow();

        Assert.Contains("uses: ./.github/workflows/deploy.yml", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: ./.github/workflows/dev-env-power.yml", workflow, StringComparison.Ordinal);
    }

    // And deploy.yml has to actually be callable, or the reuse above fails at workflow-parse time with an
    // error that names nothing useful.
    [Fact]
    public void TheDeployWorkflowIsCallable()
    {
        var deploy = File.ReadAllText(Path.Combine(RepoRoot(), ".github", "workflows", "deploy.yml"));

        Assert.Contains("workflow_call:", deploy, StringComparison.Ordinal);
        Assert.Contains("workflow_dispatch:", deploy, StringComparison.Ordinal);
    }
}
