using System.Text.RegularExpressions;
using Xunit;

namespace AgendaBuddy.AppHost.Tests;

/// <summary>
/// Structural, workflow-YAML-content tests for the shape of the F-017 CI jobs that the review gate
/// found had no regression coverage: gitleaks step presence (AC6), the docker-build-and-scan matrix
/// and its "any entry fails → job fails" guarantee (AC8), the new job's timeout and the five
/// pre-existing jobs' un-timed-ness (AC11), and the absence of any runtime/registry step (AC13).
/// No live CI run needed — same pattern as DockerAndComposeHygieneTest and PinnedThirdPartyActionsTest.
/// </summary>
public class SecurityScanAndDockerJobShapeTest
{
    private const string SecurityScanJobName = "security-scan";
    private const string DockerJobName = "docker-build-and-scan";
    private static readonly string[] ExpectedMatrixServices =
        ["AgendaBuddy.Booking.Api", "AgendaBuddy.Calendar.Api", "Customer", "AgendaBuddy.Provider.Api", "AgendaBuddy.Services.Api", "AgendaBuddy.Profession.Api", "AgendaBuddy.Identity"];
    private static readonly string[] PreExistingJobNames =
        ["changes", "build-and-test", "build-android", "build-ios", "build-mobile-tests"];

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

    private static string WorkflowContent() =>
        File.ReadAllText(Path.Combine(RepoRoot(), ".github", "workflows", "dotnet.yml"));

    /// <summary>
    /// Extracts a top-level job's body (from its "  jobName:" line up to, but not including, the
    /// next top-level "  otherJob:" line), by indentation, not by counting steps — robust to steps
    /// being added or reordered within the job.
    /// </summary>
    private static string JobBody(string workflow, string jobName)
    {
        var lines = workflow.Replace("\r\n", "\n").Split('\n');
        var jobHeaderPattern = new Regex(@"^  [A-Za-z0-9_-]+:\s*$");

        var start = Array.FindIndex(lines, line => line == $"  {jobName}:");
        Assert.True(start >= 0, $"Job '{jobName}:' not found in dotnet.yml.");

        var end = lines.Length;
        for (var i = start + 1; i < lines.Length; i++)
        {
            if (jobHeaderPattern.IsMatch(lines[i]))
            {
                end = i;
                break;
            }
        }

        return string.Join('\n', lines[start..end]);
    }

    // AC 6: the security-scan job actually has a gitleaks step, not just a dependency-audit step.
    [Fact]
    public void SecurityScanJobHasAGitleaksStep()
    {
        var body = JobBody(WorkflowContent(), SecurityScanJobName);

        Assert.Contains("gitleaks/gitleaks-action@", body);
    }

    // Review I1 (Party Review, 2026-08-26): security-scan must run on every PR unconditionally,
    // not gated on the `api`/`code` path filters — the original leak this feature exists to stop
    // recurring (ISSUE-002) lived partly under docs/pdlc/context/, exactly the path class the old
    // gate excluded.
    [Fact]
    public void SecurityScanJobRunsUnconditionallyOnEveryPullRequest()
    {
        var body = JobBody(WorkflowContent(), SecurityScanJobName);
        var ifLine = Regex.Match(body, @"^\s*if:\s*(.+)$", RegexOptions.Multiline);

        Assert.True(ifLine.Success, "security-scan must declare an explicit `if:` condition.");
        Assert.Equal("always()", ifLine.Groups[1].Value.Trim());
    }

    // AC 8 (part 1): the matrix covers exactly the seven remaining services.
    [Fact]
    public void DockerJobMatrixCoversAllSevenRemainingServices()
    {
        var body = JobBody(WorkflowContent(), DockerJobName);
        var matrixLine = Regex.Match(body, @"service:\s*\[([^\]]+)\]");

        Assert.True(matrixLine.Success, "Could not find 'service: [...]' matrix declaration.");

        var actualServices = matrixLine.Groups[1].Value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(ExpectedMatrixServices.OrderBy(s => s), actualServices.OrderBy(s => s));
    }

    // AC 8 (part 2): nothing in the job suppresses a matrix entry's failure from failing the job overall.
    // GitHub Actions' default matrix behavior already fails the job if any entry fails; the one way this
    // job could quietly defeat that is `continue-on-error: true` on a step or the job itself.
    [Fact]
    public void DockerJobDoesNotSuppressMatrixEntryFailures()
    {
        var body = JobBody(WorkflowContent(), DockerJobName);

        Assert.DoesNotContain("continue-on-error: true", body, StringComparison.Ordinal);
    }

    // AC 11: the new job carries a 10-minute timeout, and none of the five pre-existing jobs gained one.
    [Fact]
    public void DockerJobHasATenMinuteTimeoutAndPreExistingJobsAreUntouched()
    {
        var workflow = WorkflowContent();
        var dockerBody = JobBody(workflow, DockerJobName);

        Assert.Contains("timeout-minutes: 10", dockerBody);

        foreach (var jobName in PreExistingJobNames)
        {
            var body = JobBody(workflow, jobName);
            Assert.DoesNotContain("timeout-minutes:", body, StringComparison.Ordinal);
        }
    }

    // AC 13: no docker run / health check / registry push anywhere in the image-build job.
    [Fact]
    public void DockerJobNeverRunsOrPushesTheImageItBuilds()
    {
        var body = JobBody(WorkflowContent(), DockerJobName).ToLowerInvariant();

        Assert.DoesNotContain("docker run", body, StringComparison.Ordinal);
        Assert.DoesNotContain("healthcheck", body, StringComparison.Ordinal);
        Assert.DoesNotContain("health check", body, StringComparison.Ordinal);
        Assert.DoesNotContain("docker push", body, StringComparison.Ordinal);
        Assert.DoesNotContain("docker/login-action", body, StringComparison.Ordinal);
    }
}
