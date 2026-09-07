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
    /// ⚠️ The deploy machinery triggers a deploy, because a change to it can only be verified by running one.
    /// </summary>
    /// <remarks>
    /// These were excluded, so the fix for CI 358 — a concurrency self-deadlock and a stripped OIDC
    /// permission, both of which failed the job before it started and produced no log — merged to <c>main</c>
    /// without the deploy path ever running once. The defects had reached <c>main</c> by the same route. A
    /// change to how deploying works now proves itself on the merge that makes it.
    /// </remarks>
    [Theory]
    [InlineData("'.github/workflows/deploy.yml'")]
    [InlineData("'.github/workflows/dev-redeploy.yml'")]
    [InlineData("'.github/workflows/dev-env-power.yml'")]
    public void TheDeployMachineryIsInTheDeployableFilter(string entry)
    {
        Assert.Contains(entry, DeployableFilter(), StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>dotnet.yml</c> is deliberately absent: the stage inside it is a handful of <c>if:</c>/<c>needs:</c>
    /// lines guarded by this very suite, and including the file would make every CI edit of any kind deploy.
    /// </summary>
    [Fact]
    public void TheCiWorkflowItselfDoesNotTriggerADeploy()
    {
        Assert.DoesNotContain("'.github/workflows/dotnet.yml'", DeployableFilter(), StringComparison.Ordinal);
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
    /// ⚠️ <c>id-token: write</c> must be granted at <b>workflow</b> level, not only on the deploy job.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A job may narrow the workflow-level grant but cannot escalate beyond it, and <c>id-token</c> in
    /// particular is only available where the workflow level allows it. Run 358 proved it: the deploy job's
    /// own <c>permissions</c> block took effect for <c>contents</c> (workflow-level <c>pull-requests: read</c>
    /// was gone from the nested job) while <c>id-token: write</c> was silently stripped, leaving
    /// <c>Contents: read, Metadata: read</c> and no way to exchange an OIDC token with Azure.
    /// </para>
    /// <para>
    /// <b>The previous version of this test asserted that the string <c>id-token: write</c> appeared anywhere
    /// in the file, and passed while the permission was ineffective.</b> That is the lesson worth keeping:
    /// asserting the text is not asserting the effect. This checks the <c>on:</c>-adjacent workflow-level
    /// block specifically.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheOidcPermissionIsGrantedAtWorkflowLevel()
    {
        var lines = Ci().Split('\n');

        // The workflow-level block is the one at column zero, before `jobs:`.
        var start = Array.FindIndex(lines, l => l.StartsWith("permissions:", StringComparison.Ordinal));
        Assert.True(start >= 0, "dotnet.yml has no workflow-level `permissions:` block.");

        var jobs = Array.FindIndex(lines, l => l.StartsWith("jobs:", StringComparison.Ordinal));
        Assert.True(start < jobs, "The `permissions:` block found is not the workflow-level one.");

        var end = Array.FindIndex(lines, start + 1, l =>
            l.Length > 0 && !char.IsWhiteSpace(l[0]) && !l.StartsWith("#", StringComparison.Ordinal));
        var block = string.Join('\n', lines[start..(end < 0 ? jobs : end)]);

        Assert.Contains("id-token: write", block, StringComparison.Ordinal);
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
        // The restore is conditional on the schedule's own window, not unconditional: starting the
        // environment out of hours would defeat the cost control dev-env-schedule.yml exists for.
        Assert.Contains("start_after", redeploy, StringComparison.Ordinal);
    }

    /// <summary>
    /// The redeploy sequence must not provision <b>by default</b>, and must still let an operator ask it to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two failure modes, in opposite directions, and this holds both. Provisioning on every merge to main
    /// would re-apply infrastructure for no reason. But <c>provision</c> hardcoded to <c>false</c> — which is
    /// what this was — made a whole class of fix unreachable without editing the workflow: <c>azd deploy</c>
    /// pushes images and nothing else, so a parameter or secret corrected at its source does not reach the
    /// container apps until a provision run. A corrupted push credential stayed broken in dev through several
    /// successful deploys for exactly that reason.
    /// </para>
    /// <para>
    /// Asserted on the default rather than on a literal <c>provision: false</c>, because the passthrough
    /// expression is the whole point and a literal cannot express "false unless asked".
    /// </para>
    /// </remarks>
    [Fact]
    public void TheRedeploySequenceDefaultsToNoProvisionButExposesIt()
    {
        var redeploy = Workflow("dev-redeploy.yml");

        // Declared on both triggers — the pipeline caller and the human one. Anchored to the input
        // indentation, or the report job's own prose about provisioning counts as a declaration.
        Assert.Equal(2, CountOccurrences(redeploy, "\n      provision:\n"));

        // Defaulting to false is what keeps a merge to main an application-only deploy.
        Assert.Equal(2, CountOccurrences(redeploy, "\n        default: false\n"));

        // Passed through to deploy.yml, never pinned to a constant.
        Assert.Contains("provision: ${{ inputs.provision || false }}", redeploy, StringComparison.Ordinal);
        Assert.DoesNotContain("\n      provision: true", redeploy, StringComparison.Ordinal);

        // deploy.yml has to accept it on the callable trigger, or the passthrough silently does nothing.
        var deploy = Workflow("deploy.yml");
        Assert.Contains("workflow_call:", deploy, StringComparison.Ordinal);
        Assert.Contains("provision:", deploy, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        for (var i = haystack.IndexOf(needle, StringComparison.Ordinal);
             i >= 0;
             i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// ⚠️ The sequence and the deploy it calls must be in <b>different</b> concurrency groups.
    /// </summary>
    /// <remarks>
    /// <para>
    /// They were originally the same, on the reasoning that a dispatch, a pipeline run and a manual deploy
    /// should all serialise together. That self-deadlocks: <c>dev-redeploy.yml</c> acquires the group and then
    /// calls <c>deploy.yml</c>, which requests the same one — and it can never be released, because releasing
    /// it is what the parent is waiting on the child to allow. GitHub fails the impossible pending job with no
    /// log and no check run, so run 358 showed every visible job green and the run red.
    /// </para>
    /// <para>
    /// Distinct groups keep the guarantee: two redeploys serialise against each other, and the inner deploy
    /// still serialises against any directly dispatched <c>deploy.yml</c>.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheSequenceAndTheDeployDoNotShareAConcurrencyGroup()
    {
        var redeployGroup = ConcurrencyGroup("dev-redeploy.yml");
        var deployGroup = ConcurrencyGroup("deploy.yml");

        Assert.NotEqual(deployGroup, redeployGroup);
    }

    // Neither may cancel a run in flight: a half-applied Terraform/azd run is worse than a queued one.
    [Theory]
    [InlineData("dev-redeploy.yml")]
    [InlineData("deploy.yml")]
    public void NeitherTheSequenceNorTheDeployCancelsInProgress(string workflow)
    {
        Assert.Contains("cancel-in-progress: false", Workflow(workflow), StringComparison.Ordinal);
    }

    /// <summary>
    /// The workflow-level <c>concurrency.group</c>, with <c>${{ … }}</c> expressions normalised away —
    /// see <see cref="DevEnvDriftTest.ConcurrencyGroup"/> for why comparing the raw text is not enough.
    /// </summary>
    private static string ConcurrencyGroup(string name) => DevEnvDriftTest.ConcurrencyGroup(name);

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

    // ── A deploy without a provision recovers the provisioning outputs ─────────────────────────────

    /// <summary>
    /// A <c>provision: false</c> deploy must refresh the azd environment before deploying, and must do so
    /// <b>before</b> <c>azd deploy</c> runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the mode both <c>dev-redeploy.yml</c> and .NET CI's <c>deploy-dev</c> stage use, and it had
    /// never succeeded. <c>.azure/</c> is gitignored and the runner is ephemeral, so the workflow's
    /// <c>azd env new</c> creates an empty environment every time — no container registry endpoint, no
    /// Container Apps environment id — and azd failed at "logging in to registry" on the first service.
    /// </para>
    /// <para>
    /// Only the one deploy that ran with <c>provision: true</c> ever went green, because provisioning
    /// writes those outputs itself. That is precisely why this needs a test: the defect is invisible in
    /// the mode a human dispatches by hand and fatal in the two modes that run unattended.
    /// </para>
    /// </remarks>
    [Fact]
    public void ADeployWithoutAProvisionRefreshesTheAzdEnvironmentFirst()
    {
        var lines = Workflow("deploy.yml").Split('\n');

        // Anchored on the step header, NOT on the string "azd env refresh": the comment above the step
        // explains the command by name, so matching the bare command found the comment and reported the
        // step as correctly ordered even after it had been moved below `azd deploy`.
        var refresh = Array.FindIndex(lines,
            l => l.TrimStart().StartsWith("- name: azd env refresh", StringComparison.Ordinal));
        Assert.True(refresh >= 0, "deploy.yml never refreshes the azd environment, so a `provision: false` "
                                  + "run has no container registry endpoint and cannot push an image.");

        var deploy = Array.FindIndex(lines, l => l.Trim() == "run: azd deploy --no-prompt");
        Assert.True(deploy >= 0, "deploy.yml no longer runs `azd deploy --no-prompt`.");
        Assert.True(refresh < deploy, "the refresh has to precede `azd deploy` to be of any use.");

        // Gated off when provisioning, which writes the same outputs itself.
        var gate = Array.FindIndex(lines[refresh..], l => l.Contains("if:", StringComparison.Ordinal));
        Assert.True(gate >= 0, "the refresh step declares no `if:` condition.");
        Assert.Contains("!inputs.provision", lines[refresh + gate], StringComparison.Ordinal);
    }

    /// <summary>
    /// The refresh asserts the registry endpoint actually arrived, rather than trusting its own exit code.
    /// </summary>
    /// <remarks>
    /// A refresh that reports success while producing no endpoint reproduces the original failure ~40
    /// packaging seconds later, under a message that blames docker options rather than the environment.
    /// </remarks>
    [Fact]
    public void TheRefreshVerifiesTheRegistryEndpointIsPresent()
    {
        Assert.Contains("AZURE_CONTAINER_REGISTRY_ENDPOINT", Workflow("deploy.yml"), StringComparison.Ordinal);
    }
}
