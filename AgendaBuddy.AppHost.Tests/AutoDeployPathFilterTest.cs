using System.Text.RegularExpressions;
using Xunit;

namespace AgendaBuddy.AppHost.Tests;

/// <summary>
/// The pipeline's last stage deploys the dev environment when a merge to main changes deployed behaviour.
/// This holds the parts of that decision that fail silently.
/// </summary>
/// <remarks>
/// <para>
/// <b>The failure this exists for is silent in the worst direction.</b> The decision is driven by the
/// <c>deployable</c> path filter in <c>dotnet.yml</c>'s <c>changes</c> job. Rename or add a service and
/// forget that filter, and merges touching it stop triggering a deploy — no error, no red check, just a
/// dev environment drifting behind main until somebody reports a bug that was already fixed. CLAUDE.md
/// records that every path filter in that job had to be updated for each of F-020's 12 project renames;
/// this is the same trap with a quieter symptom, so the expected entries are derived from the AppHost's
/// own resource graph rather than restated.
/// </para>
/// <para>
/// Structural, YAML-as-text, no CI run needed — the same pattern as
/// <see cref="SecurityScanAndDockerJobShapeTest"/> and <see cref="DockerAndComposeHygieneTest"/>.
/// </para>
/// </remarks>
public class AutoDeployPathFilterTest
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

    private static string Ci() => Workflow("dotnet.yml");

    /// <summary>
    /// The <c>deployable:</c> filter's own entries, isolated from every other filter and from the
    /// surrounding prose — several of these paths are *discussed* in comments, so a whole-file search
    /// would pass on a mention and prove nothing.
    /// </summary>
    private static string DeployableFilter()
    {
        var lines = Ci().Split('\n');

        var start = Array.FindIndex(lines, l => l.TrimEnd() == "            deployable:");
        Assert.True(start >= 0,
            "dotnet.yml's `changes` job no longer declares a `deployable:` filter — the deploy stage's "
            + "path decision has moved or been deleted, and this test can no longer see what it matches.");

        // Runs until the next line at the filter-name indent that is not itself an entry or a comment.
        var end = start + 1;
        while (end < lines.Length)
        {
            var line = lines[end];
            var trimmed = line.TrimStart();
            var indent = line.Length - trimmed.Length;

            if (trimmed.Length > 0 && indent <= 12 && !trimmed.StartsWith('-') && !trimmed.StartsWith('#'))
                break;

            end++;
        }

        return string.Join('\n', lines[start..end]);
    }

    /// <summary>
    /// Every project the AppHost declares as a resource — the seven services plus the Gateway — read from
    /// <c>Projects.AgendaBuddy_*</c> in the app model rather than from a second hardcoded list, so adding a
    /// service to the graph is what makes this test demand a filter entry.
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
    public void EveryAppHostDeclaredServiceIsInTheDeployableFilter(string projectDirectory)
    {
        Assert.Contains($"'{projectDirectory}/**'", DeployableFilter(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Sanity check on the discovery above: if the regex ever stops matching, every
    /// <see cref="EveryAppHostDeclaredServiceIsInTheDeployableFilter"/> case would vanish and the suite
    /// would go green having asserted nothing at all.
    /// </summary>
    [Fact]
    public void TheAppHostGraphYieldsTheEightDeployedProjects()
    {
        Assert.Equal(8, DeployedProjectDirectories().Count());
    }

    /// <summary>
    /// The shared projects every service compiles in, and the infrastructure description. A change to
    /// <c>AgendaBuddy.Library</c> changes all eight apps' behaviour, so it has to trigger a deploy even
    /// though no service directory was touched — the same class of omission that once let a
    /// JWT-validation change run zero CI jobs, because <c>AgendaBuddy.Library.ServerAuth</c> was in no
    /// filter at all.
    /// </summary>
    [Theory]
    [InlineData("'AgendaBuddy.Library/**'")]
    [InlineData("'AgendaBuddy.Library.ServerAuth/**'")]
    [InlineData("'AgendaBuddy.EventAndCommands/**'")]
    [InlineData("'AgendaBuddy.ServiceDefaults/**'")]
    [InlineData("'AgendaBuddy.AppHost/**'")]
    [InlineData("'Directory.Build.props'")]
    [InlineData("'azure.yaml'")]
    [InlineData("'infra/terraform/**'")]
    public void TheSharedAndInfrastructurePathsAreInTheDeployableFilter(string entry)
    {
        Assert.Contains(entry, DeployableFilter(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Test projects and the mobile client must NOT trigger a deploy. Neither changes deployed behaviour,
    /// and a deploy is a full Terraform + azd run plus eight container builds — the mobile client ships
    /// through TestFlight, not through azd.
    /// </summary>
    [Theory]
    [InlineData("AgendaBuddy.Library.Tests/")]
    [InlineData("AgendaBuddy.Booking.Tests/")]
    [InlineData("AgendaBuddy.IntegrationTests/")]
    [InlineData("AgendaBuddy.MobileApp/")]
    [InlineData("AgendaBuddy.MobileApp.Tests/")]
    public void NonDeployablePathsAreNotInTheDeployableFilter(string path)
    {
        Assert.DoesNotContain($"'{path}", DeployableFilter(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The filter has to be exposed as a job output, or the deploy stage cannot read it — and an
    /// unresolvable <c>needs.changes.outputs.deployable</c> evaluates to empty, which silently never
    /// equals <c>'true'</c>. That is a deploy stage that looks wired and never fires.
    /// </summary>
    [Fact]
    public void TheDeployableFilterIsExposedAsAJobOutputAndReadByTheDeployStage()
    {
        var ci = Ci();

        Assert.Contains("deployable: ${{ steps.filter.outputs.deployable }}", ci, StringComparison.Ordinal);
        Assert.Contains("needs.changes.outputs.deployable == 'true'", ci, StringComparison.Ordinal);
    }

    // ── The deploy stage's own guardrails ──────────────────────────────────────────────────────────

    /// <summary>
    /// It is a stage of the pipeline, reusing the same sequence the on-demand button runs.
    /// </summary>
    [Fact]
    public void TheDeployIsAPipelineStageThatReusesTheRedeploySequence()
    {
        var ci = Ci();

        Assert.Contains("deploy-dev:", ci, StringComparison.Ordinal);
        Assert.Contains("uses: ./.github/workflows/dev-redeploy.yml", ci, StringComparison.Ordinal);
    }

    /// <summary>
    /// Only on a merge to main. A pull_request run must never deploy — the same workflow serves both
    /// events, so this is the one condition standing between a PR and the dev environment.
    /// </summary>
    [Fact]
    public void ItDeploysOnlyOnAPushToMain()
    {
        var ci = Ci();

        Assert.Contains("github.event_name == 'push'", ci, StringComparison.Ordinal);
        Assert.Contains("github.ref == 'refs/heads/main'", ci, StringComparison.Ordinal);
    }

    /// <summary>
    /// A cancelled pipeline must not deploy, and a failed gate must not either.
    /// </summary>
    /// <remarks>
    /// <c>!cancelled()</c> rather than <c>always()</c>, and every gate compared against
    /// <c>!= 'failure'</c> rather than <c>== 'success'</c>: most of these jobs are filter-gated, so a skip
    /// means "not relevant to this change" and must not block, while a failure in any of them — mobile
    /// included — must.
    /// </remarks>
    [Fact]
    public void ACancelledOrFailedPipelineDoesNotDeploy()
    {
        var ci = Ci();

        Assert.Contains("!cancelled()", ci, StringComparison.Ordinal);

        foreach (var gate in new[]
                 {
                     "build-and-test", "security-scan", "docker-build-and-scan", "integration",
                     "build-android", "build-ios", "build-mobile-tests", "terraform-validate"
                 })
        {
            Assert.Contains($"needs.{gate}.result != 'failure'", ci, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// ⚠️ The pipeline must not cancel a superseded run on main, because that run can be mid-deploy.
    /// </summary>
    /// <remarks>
    /// A half-applied <c>terraform apply</c>/<c>azd deploy</c> is materially worse than a queued run. PRs
    /// keep the superseding behaviour, which is the reason the concurrency group exists at all — so this
    /// asserts the expression, not a bare <c>false</c>.
    /// </remarks>
    [Fact]
    public void TheCiPipelineDoesNotCancelInProgressRunsOnMain()
    {
        Assert.Contains(
            "cancel-in-progress: ${{ github.ref != 'refs/heads/main' }}", Ci(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A kill switch that stops the automation without a code change, and its polarity matters: only the
    /// literal string <c>false</c> disables it, so an unset or mistyped variable deploys. The earlier
    /// opt-IN version failed the other way — an automation that looked wired and did nothing.
    /// </summary>
    [Fact]
    public void ThereIsAKillSwitchAndItFailsTowardsDeploying()
    {
        var ci = Ci();

        Assert.Contains("vars.AUTO_DEPLOY_DEV != 'false'", ci, StringComparison.Ordinal);
        Assert.DoesNotContain("vars.AUTO_DEPLOY_DEV == 'true'", ci, StringComparison.Ordinal);
    }

    /// <summary>
    /// The deploy exchanges a GitHub OIDC token for an Azure one, and <c>dotnet.yml</c>'s workflow-level
    /// permissions are read-only — job-level permissions replace rather than extend them, so the stage has
    /// to grant <c>id-token: write</c> itself or the Azure login fails with a token it never received.
    /// </summary>
    [Fact]
    public void TheDeployStageGrantsItselfTheOidcPermission()
    {
        Assert.Contains("id-token: write", Ci(), StringComparison.Ordinal);
    }

    // ── The sequence, and the on-demand path ───────────────────────────────────────────────────────

    /// <summary>
    /// Still on demand: the same three-stage sequence is dispatchable, and <c>deploy.yml</c> keeps its own
    /// dispatch — which is the only way to run with <c>provision: true</c>.
    /// </summary>
    [Fact]
    public void TheSequenceAndTheDeployAreBothStillRunnableOnDemand()
    {
        var redeploy = Workflow("dev-redeploy.yml");
        var deploy = Workflow("deploy.yml");

        Assert.Contains("workflow_dispatch:", redeploy, StringComparison.Ordinal);
        Assert.Contains("workflow_call:", redeploy, StringComparison.Ordinal);
        Assert.Contains("workflow_dispatch:", deploy, StringComparison.Ordinal);
        Assert.Contains("workflow_call:", deploy, StringComparison.Ordinal);
    }

    /// <summary>
    /// Three stages, not two, and the third is load-bearing.
    /// </summary>
    /// <remarks>
    /// <c>dev-env-stop</c> sets <c>minReplicas=0</c> and <c>azd deploy</c> does not reset the scale rule —
    /// only <c>azd provision</c> does, and it is deliberately <c>false</c> for an application-code deploy.
    /// So without the restore, a stop→deploy leaves the new code at zero replicas until the next scheduled
    /// weekday 09:00.
    /// </remarks>
    [Fact]
    public void TheRedeploySequenceStopsDeploysAndRestores()
    {
        var redeploy = Workflow("dev-redeploy.yml");

        Assert.Contains("action: stop", redeploy, StringComparison.Ordinal);
        Assert.Contains("action: start", redeploy, StringComparison.Ordinal);
        Assert.Contains("uses: ./.github/workflows/deploy.yml", redeploy, StringComparison.Ordinal);
        Assert.Contains("provision: false", redeploy, StringComparison.Ordinal);
        // The restore is conditional on the schedule's own window, not unconditional: starting the
        // environment out of hours would defeat the cost control dev-env-schedule.yml exists for.
        Assert.Contains("start_after", redeploy, StringComparison.Ordinal);
    }

    /// <summary>
    /// Never cancels, and shares deploy.yml's group so a dispatch, a pipeline run and a manual deploy
    /// cannot overlap.
    /// </summary>
    [Fact]
    public void TheRedeploySequenceNeverCancelsAndSharesTheDeployGroup()
    {
        var redeploy = Workflow("dev-redeploy.yml");

        Assert.Contains("group: deploy-${{ inputs.environment }}", redeploy, StringComparison.Ordinal);
        Assert.Contains("cancel-in-progress: false", redeploy, StringComparison.Ordinal);
    }

    // ── Push credentials reach a deployed environment ──────────────────────────────────────────────

    /// <summary>
    /// The two push parameters have to travel all the way from a GitHub secret to a container app's
    /// environment, and every link is in a different file.
    /// </summary>
    /// <remarks>
    /// They previously reached none of it: <c>AppHostWiring</c> declared them only when a value was already
    /// in <c>builder.Configuration</c>, which is never true while the AppHost is being published, so the
    /// parameters never entered the generated Bicep and every deployed environment resolved
    /// <c>UnconfiguredPushSender</c> — a backend that could not push, with nothing reporting why.
    /// </remarks>
    [Fact]
    public void ThePushCredentialsAreWiredFromSecretToKeyVaultToAzdParameter()
    {
        var deploy = Workflow("deploy.yml");
        var variables = File.ReadAllText(
            Path.Combine(RepoRoot(), "infra", "terraform", "environment", "variables.tf"));
        var main = File.ReadAllText(
            Path.Combine(RepoRoot(), "infra", "terraform", "environment", "main.tf"));

        // GitHub → Terraform
        Assert.Contains("TF_VAR_push_firebase_project_id", deploy, StringComparison.Ordinal);
        Assert.Contains("TF_VAR_push_service_account_json", deploy, StringComparison.Ordinal);

        // Terraform → Key Vault
        Assert.Contains("variable \"push_firebase_project_id\"", variables, StringComparison.Ordinal);
        Assert.Contains("variable \"push_service_account_json\"", variables, StringComparison.Ordinal);
        Assert.Contains("name         = \"push-firebase-project-id\"", main, StringComparison.Ordinal);
        Assert.Contains("name         = \"push-service-account-json\"", main, StringComparison.Ordinal);

        // Key Vault → azd parameter, under the Aspire parameter names with hyphens as underscores.
        Assert.Contains("\"push-firebase-project-id\": (\"push_firebase_project_id\", False)", deploy, StringComparison.Ordinal);
        Assert.Contains("\"push-service-account-json\": (\"push_service_account_json\", False)", deploy, StringComparison.Ordinal);
    }

    /// <summary>
    /// An optional secret that is absent must still be supplied to azd as an empty string, not omitted.
    /// </summary>
    /// <remarks>
    /// The Cloud shape declares these parameters unconditionally, and <c>azd provision --no-prompt</c>
    /// fails on a declared parameter with no value. Empty is exactly what <c>PushOptions</c> and
    /// <c>EmailOptions</c> read as "not configured", so an environment without push credentials deploys and
    /// simply logs that push is off.
    /// </remarks>
    [Fact]
    public void AnAbsentOptionalSecretIsSuppliedAsEmptyRatherThanOmitted()
    {
        Assert.Contains("parameters[param] = \"\"", Workflow("deploy.yml"), StringComparison.Ordinal);
    }
}
