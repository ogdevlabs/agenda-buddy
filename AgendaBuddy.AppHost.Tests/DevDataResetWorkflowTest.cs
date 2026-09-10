using Xunit;

namespace AgendaBuddy.AppHost.Tests;

public class DevDataResetWorkflowTest
{
    private static string Workflow()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "agenda-buddy.sln")))
            current = current.Parent;

        Assert.NotNull(current);
        return File.ReadAllText(Path.Combine(current.FullName, ".github", "workflows", "dev-data-reset.yml"));
    }

    [Fact]
    public void ResetIsManualDevOnlyAndRequiresAnExactConfirmation()
    {
        var workflow = Workflow();

        Assert.Contains("workflow_dispatch:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("workflow_call:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("schedule:", workflow, StringComparison.Ordinal);
        Assert.Contains("environment: dev", workflow, StringComparison.Ordinal);
        Assert.Contains("DELETE DEV USER DATA", workflow, StringComparison.Ordinal);
        Assert.Contains("refs/heads/main", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("inputs.environment", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("echo \"- reason: ${{ inputs.reason }}\"", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void ResetStopsAppsPurgesBothDatabasesAndRestoresOnlyInsideTheSchedule()
    {
        var workflow = Workflow();

        Assert.Contains("action: stop", workflow, StringComparison.Ordinal);
        Assert.Contains("action: start", workflow, StringComparison.Ordinal);
        Assert.Contains("az containerapp replica list", workflow, StringComparison.Ordinal);
        Assert.Contains("refusing to purge", workflow, StringComparison.Ordinal);
        Assert.Contains("always()", workflow, StringComparison.Ordinal);
        Assert.Contains("start_after", workflow, StringComparison.Ordinal);
        Assert.Contains("agenda-buddy-connection", workflow, StringComparison.Ordinal);
        Assert.Contains("identity-db-connection", workflow, StringComparison.Ordinal);
        Assert.Contains("agenda_buddy", workflow, StringComparison.Ordinal);
        Assert.Contains("IdentityDb", workflow, StringComparison.Ordinal);
        Assert.Contains("getCollectionNames", workflow, StringComparison.Ordinal);
        Assert.Contains("deleteMany({})", workflow, StringComparison.Ordinal);
        Assert.Contains("professions", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("dropDatabase", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("drop()", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void ResetImmediatelyRevokesPreexistingAccessTokens()
    {
        var workflow = Workflow();

        Assert.Contains("__all_tokens_before__", workflow, StringComparison.Ordinal);
        Assert.Contains("revoked_before", workflow, StringComparison.Ordinal);
        Assert.Contains("replaceOne", workflow, StringComparison.Ordinal);
    }
}
