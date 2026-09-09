using System.Text.RegularExpressions;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

public partial class ViewModelErrorLocalizationTest
{
    [Fact]
    public void ErrorMessageAssignmentsDoNotContainVisibleEnglishLiterals()
    {
        var app = Path.Combine(RepoRoot(), "AgendaBuddy.MobileApp");
        var files = Directory.EnumerateFiles(Path.Combine(app, "ViewModels"), "*.cs")
            .Concat(Directory.EnumerateFiles(Path.Combine(app, "Views"), "*.xaml.cs"));

        var regressions = files
            .SelectMany(file => HardcodedAssignment().Matches(File.ReadAllText(file))
                .Select(match => $"{Path.GetRelativePath(app, file)}: {match.Value}"))
            .ToList();

        Assert.True(regressions.Count == 0,
            "Visible error assignments must use AppResources: " + string.Join(", ", regressions));
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "agenda-buddy.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repo root (agenda-buddy.sln) walking up from {AppContext.BaseDirectory}.");
    }

    [GeneratedRegex("(?:ErrorMessage|NotesErrorMessage|PayErrorMessage)\\s*=\\s*\\$?\"")]
    private static partial Regex HardcodedAssignment();
}