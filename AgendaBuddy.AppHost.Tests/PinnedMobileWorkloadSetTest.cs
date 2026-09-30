using System.Text.RegularExpressions;
using Xunit;

namespace AgendaBuddy.AppHost.Tests;

/// <summary>
/// A MAUI workload set binds one Xcode SDK. Installing "latest" lets a new set arrive before the runner image
/// ships its Xcode, and the signed App Store archive then fails MT4162 while the unsigned CI build — which skips
/// the linker step that checks — stays green. Every workload install must name a set, and the release workflow
/// must use the same one CI builds with, or CI no longer tells you whether the release will build.
/// </summary>
public class PinnedMobileWorkloadSetTest
{
    private static readonly string[] Workflows = ["dotnet.yml", "ios-release.yml"];

    [Fact]
    public void EveryMobileWorkloadInstallPinsTheSameWorkloadSet()
    {
        var installs = Workflows
            .SelectMany(file => File.ReadAllLines(Path.Combine(RepoRoot(), ".github", "workflows", file))
                .Select((line, index) => (file, lineNumber: index + 1, line: line.Trim())))
            .Where(entry => entry.line.StartsWith("run: dotnet workload install maui-", StringComparison.Ordinal))
            .ToList();

        Assert.Contains(installs, entry => entry.file == "ios-release.yml");
        Assert.Contains(installs, entry => entry.file == "dotnet.yml");

        var versions = installs
            .Select(entry => (entry, version: Regex.Match(entry.line, @"--version\s+(\S+)").Groups[1].Value))
            .ToList();

        var unpinned = versions.Where(v => v.version.Length == 0)
            .Select(v => $"{v.entry.file}:{v.entry.lineNumber}: '{v.entry.line}' installs the latest workload set.")
            .ToList();
        Assert.True(unpinned.Count == 0, string.Join('\n', unpinned));

        Assert.Single(versions.Select(v => v.version).Distinct());
    }

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
