using System.Text.RegularExpressions;
using Xunit;

namespace AgendaBuddy.AppHost.Tests;

/// <summary>
/// [security] (T-001, docs/pdlc/design/container-and-cd-hardening/threat-model.md) — unpinned
/// third-party GitHub Actions allow a supply-chain substitution attack if a mutable tag/branch is
/// later re-pointed to malicious code. Structural, file-content-based; no live CI run needed.
/// </summary>
public class PinnedThirdPartyActionsTest
{
    private static readonly string[] ActionsRequiringPin = ["gitleaks-action", "trivy-action"];

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

    [Fact]
    public void T001_GitleaksAndTrivyActionsArePinnedToFullCommitShas()
    {
        var workflowPath = Path.Combine(RepoRoot(), ".github", "workflows", "dotnet.yml");
        var lines = File.ReadAllLines(workflowPath);

        var usesLines = lines
            .Select((line, index) => (line: line.Trim(), lineNumber: index + 1))
            .Where(entry => entry.line.StartsWith("uses:", StringComparison.OrdinalIgnoreCase)
                         && ActionsRequiringPin.Any(action => entry.line.Contains(action, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.NotEmpty(usesLines); // the guard must actually find something to guard.

        var unpinned = new List<string>();

        foreach (var (line, lineNumber) in usesLines)
        {
            var match = Regex.Match(line, @"@([^\s#]+)");

            if (!match.Success || !Regex.IsMatch(match.Groups[1].Value, "^[0-9a-f]{40}$"))
            {
                unpinned.Add($"dotnet.yml:{lineNumber}: '{line}' does not reference a 40-character hex commit SHA.");
            }
        }

        Assert.True(unpinned.Count == 0, "Unpinned third-party Action(s):\n" + string.Join('\n', unpinned));
    }
}
