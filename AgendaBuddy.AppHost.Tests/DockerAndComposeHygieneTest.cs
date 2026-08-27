using System.Text.RegularExpressions;
using Xunit;

namespace AgendaBuddy.AppHost.Tests;

/// <summary>
/// Structural, file-tree-based regression tests — no container runtime needed. Whether an image
/// actually runs is verified live (F-017-T06/T07); these tests only pin the repository shape.
/// </summary>
public class DockerAndComposeHygieneTest
{
    private static readonly string[] DeletedProjects = ["AgendaBuddy.Library", "AgendaBuddy.Kafka", "EventAndCommands"];
    private static readonly string[] DeletedComposeServices = ["events", "kafka-library", "common-library"];

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

    // Requirement 1 / AC 1: the three class-library Dockerfiles no longer exist.
    [Theory]
    [MemberData(nameof(DeletedProjectNames))]
    public void ClassLibraryDockerfileDoesNotExist(string projectName)
    {
        var path = Path.Combine(RepoRoot(), projectName, "Dockerfile");

        Assert.False(File.Exists(path), $"{path} should have been deleted — {projectName} is a class library with no entrypoint.");
    }

    public static IEnumerable<object[]> DeletedProjectNames() =>
        DeletedProjects.Select(name => new object[] { name });

    // Requirement 1 / AC 2: neither compose file declares the three no-op service blocks.
    [Theory]
    [InlineData("docker-compose.yml")]
    [InlineData("docker-compose.override.yml")]
    public void ComposeFileDeclaresNoDeletedClassLibraryServices(string composeFileName)
    {
        var path = Path.Combine(RepoRoot(), composeFileName);
        var content = File.ReadAllText(path);

        foreach (var serviceName in DeletedComposeServices)
        {
            Assert.False(
                Regex.IsMatch(content, $@"(?m)^\s{{2}}{Regex.Escape(serviceName)}:\s*$"),
                $"{composeFileName} still declares a '{serviceName}:' service block.");
        }
    }

    // Requirement 2 / AC 3: generalized so this defect class cannot recur under a different filename —
    // scans every Dockerfile in the repo, not just the three deleted above.
    [Fact]
    public void NoDockerfileMismatchesItsFinalStageRuntimeMajorVersionAgainstItsBuildStageSdkMajorVersion()
    {
        var root = RepoRoot();
        var dockerfiles = Directory
            .EnumerateFiles(root, "Dockerfile", SearchOption.AllDirectories)
            .Where(path => Path.GetRelativePath(root, path)
                .Split(Path.DirectorySeparatorChar)
                .All(segment => segment is not ("bin" or "obj") && !segment.StartsWith('.')))
            .ToList();

        // The repo must actually have Dockerfiles for this guard to mean anything.
        Assert.NotEmpty(dockerfiles);

        var mismatches = new List<string>();

        foreach (var path in dockerfiles)
        {
            var fromLines = File.ReadAllLines(path)
                .Select(line => line.Trim())
                .Where(line => line.StartsWith("FROM ", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var sdkLine = fromLines.FirstOrDefault(line => line.Contains("/dotnet/sdk:", StringComparison.OrdinalIgnoreCase));
            var runtimeLine = fromLines.FirstOrDefault(line =>
                line.Contains("/dotnet/aspnet:", StringComparison.OrdinalIgnoreCase)
                || line.Contains("/dotnet/runtime:", StringComparison.OrdinalIgnoreCase));

            // Not a multi-stage .NET SDK/runtime Dockerfile in the shape this guard understands — skip.
            if (sdkLine is null || runtimeLine is null)
            {
                continue;
            }

            var sdkMajor = MajorVersionOf(sdkLine);
            var runtimeMajor = MajorVersionOf(runtimeLine);

            if (sdkMajor != runtimeMajor)
            {
                mismatches.Add(
                    $"{Path.GetRelativePath(root, path)}: build stage is major {sdkMajor} ('{sdkLine}') " +
                    $"but final stage is major {runtimeMajor} ('{runtimeLine}').");
            }
        }

        Assert.True(mismatches.Count == 0, "Dockerfile runtime/SDK major-version mismatch(es):\n" + string.Join('\n', mismatches));
    }

    private static int MajorVersionOf(string fromLine)
    {
        var match = Regex.Match(fromLine, @":(\d+)\.");

        if (!match.Success)
        {
            throw new InvalidOperationException($"Could not parse an image version out of: '{fromLine}'.");
        }

        return int.Parse(match.Groups[1].Value);
    }
}
