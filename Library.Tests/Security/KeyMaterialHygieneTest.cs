using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Common.Tests.Security;

/// <summary>
/// Pins F-016 AC-3: no PEM key material is committed to this repository, and no production project
/// takes a <c>ProjectReference</c> on <c>AgendaBuddy.IntegrationTests</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this lives in <c>Library.Tests</c> rather than in the harness project it describes.</b>
/// <c>AgendaBuddy.IntegrationTests</c> is deliberately excluded from <c>agenda-buddy-backend.slnf</c>
/// (ADR-031) so the unit gate stays Docker-free, and the integration CI job does not exist yet
/// (<c>F-016-T20</c>). A secret-hygiene assertion that only runs when someone remembers a second
/// command is decoration. These two tests need no container and no harness type, so they are hosted
/// in a project the <c>api</c> CI job already runs on every pull request. Move them to the harness
/// once <c>T20</c>'s job is green and blocking, if that reads better then.
/// </para>
/// <para>
/// <b>The repository is PUBLIC.</b> A committed key is a permanent artifact: deleting it from the
/// working tree does not delete it from history — the Atlas credential in <c>ISSUE-002</c> is this
/// project's own standing proof of that. The check therefore runs against <b>tracked</b> files, and
/// fails closed if <c>git</c> cannot be reached rather than passing vacuously.
/// </para>
/// <para>
/// ⚠️ <b>The second assertion matches <c>ProjectReference</c>, not the project name.</b> Seven
/// production <c>.csproj</c> files legitimately name <c>AgendaBuddy.IntegrationTests</c> in an
/// <c>&lt;InternalsVisibleTo&gt;</c> item, added by <c>F-016-T02</c> for AC-2 (e.g.
/// <c>Booking/Booking.csproj:35</c>). That is a compile-time friend-assembly grant and pulls in no
/// code — it is not what AC-3 prohibits. A test that matched the bare string would be red forever,
/// and the tempting fix would be deleting the grant and silently breaking AC-2. The
/// <c>ProjectReferenceDetector_TreatsInternalsVisibleToAsHarmless</c> case exists to keep that
/// distinction from being "corrected" later.
/// </para>
/// </remarks>
public class KeyMaterialHygieneTest
{
    private const string HarnessProject = "AgendaBuddy.IntegrationTests";

    /// <summary>
    /// A PEM block carrying an actual base64 payload — not merely the delimiter text.
    /// </summary>
    /// <remarks>
    /// The payload requirement is what makes the check usable. Two tracked files build PEM
    /// delimiters as interpolated format strings around a runtime-generated key
    /// (<c>Identity.Tests/Helpers/RsaKeyHelper.cs</c> and
    /// <c>Library.Tests/Extensions/AuthenticationExtensionsTest.cs</c>'s
    /// <c>GenerateTestRsaPublicKeyPem</c>). Those hold no key material and must not be flagged:
    /// the interpolation braces are outside the base64 character class, so they cannot satisfy it.
    /// </remarks>
    private static readonly Regex PemBlockWithPayload = new(
        @"-----BEGIN (?:[A-Z]+ )*(?:PUBLIC|PRIVATE) KEY-----[A-Za-z0-9+/=\s]{64,}?-----END",
        RegexOptions.Compiled);

    [Fact]
    public void NoTrackedFile_ContainsPemKeyMaterial()
    {
        var root = RepositoryRoot();
        var offenders = new List<string>();

        foreach (var relativePath in TrackedFiles(root))
        {
            var fullPath = Path.Combine(root, relativePath);
            if (!File.Exists(fullPath))
            {
                // Staged deletion, or a path git knows about that this checkout does not materialise.
                continue;
            }

            string content;
            try
            {
                content = File.ReadAllText(fullPath);
            }
            catch (IOException)
            {
                continue;
            }

            var match = PemBlockWithPayload.Match(content);
            if (match.Success)
            {
                offenders.Add($"{relativePath} (at character {match.Index})");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "PEM key material is committed to this PUBLIC repository. Removing it from the working " +
            "tree does not remove it from history, so it must never land in the first place " +
            "(F-016 AC-3). Offending tracked files:" +
            Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", offenders));
    }

    [Fact]
    public void NoProductionProject_TakesAProjectReferenceOnTheIntegrationHarness()
    {
        var root = RepositoryRoot();
        var offenders = new List<string>();

        foreach (var relativePath in TrackedFiles(root).Where(IsProductionProjectFile))
        {
            var fullPath = Path.Combine(root, relativePath);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            if (HasProjectReferenceTo(XDocument.Load(fullPath), HarnessProject))
            {
                offenders.Add(relativePath);
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"A production project references {HarnessProject}, which would ship the test harness — " +
            "and Testcontainers, and the unpatchable SSH.NET advisory it drags in (ADR-030) — into a " +
            "deployable artifact (F-016 AC-3). Offending projects:" +
            Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", offenders));
    }

    [Fact]
    public void ProjectReferenceDetector_FindsAProjectReferenceOnTheHarness()
    {
        // Gives the guard above its teeth: proves it can actually fail, without committing a
        // reference to the tree to demonstrate it.
        var offending = XDocument.Parse(
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                <ProjectReference Include="..\AgendaBuddy.IntegrationTests\AgendaBuddy.IntegrationTests.csproj" />
              </ItemGroup>
            </Project>
            """);

        Assert.True(HasProjectReferenceTo(offending, HarnessProject));
    }

    [Fact]
    public void ProjectReferenceDetector_TreatsInternalsVisibleToAsHarmless()
    {
        // This is the shape all seven production services actually have (F-016 AC-2). It must not
        // be read as a reference, or AC-3 and AC-2 become impossible to satisfy at the same time.
        var friendGrantOnly = XDocument.Parse(
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                <InternalsVisibleTo Include="AgendaBuddy.IntegrationTests" />
              </ItemGroup>
            </Project>
            """);

        Assert.False(HasProjectReferenceTo(friendGrantOnly, HarnessProject));
    }

    private static bool IsProductionProjectFile(string relativePath) =>
        relativePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
        && !relativePath.EndsWith(".Tests.csproj", StringComparison.OrdinalIgnoreCase)
        && !ProjectNameOf(relativePath).Equals(HarnessProject, StringComparison.OrdinalIgnoreCase);

    private static bool HasProjectReferenceTo(XDocument project, string projectName) =>
        project.Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Any(include => ProjectNameOf(include!).Equals(projectName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The project name from a path or MSBuild <c>Include</c>, which uses <c>\</c> regardless of host OS.
    /// </summary>
    private static string ProjectNameOf(string pathOrInclude)
    {
        var fileName = pathOrInclude.Replace('\\', '/').Split('/').Last();
        return fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            ? fileName[..^".csproj".Length]
            : fileName;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var gitPath = Path.Combine(directory.FullName, ".git");

            // A directory in a normal clone; a file in a git worktree or submodule.
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"No `.git` found above {AppContext.BaseDirectory}. This test scans TRACKED files and " +
            "fails closed rather than reporting a vacuous pass (F-016 AC-3).");
    }

    private static IReadOnlyList<string> TrackedFiles(string repositoryRoot)
    {
        var startInfo = new ProcessStartInfo("git", "ls-files -z")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var git = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Could not start `git`. This test enumerates TRACKED files and fails closed rather " +
                "than reporting a vacuous pass (F-016 AC-3).");

        var stdout = git.StandardOutput.ReadToEnd();
        var stderr = git.StandardError.ReadToEnd();
        git.WaitForExit();

        if (git.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"`git ls-files` exited {git.ExitCode} in {repositoryRoot}: {stderr}");
        }

        return stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }
}
