using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Views;

/// <summary>
/// A resource string referenced from XAML through <c>x:Static</c> is shown as-is — nothing formats it — so a
/// <c>{0}</c> in its value reaches the screen literally. Strings with placeholders belong to view models.
/// </summary>
public class StaticStringPlaceholderTest
{
    private static readonly Regex StaticReference = new(@"x:Static\s+strings:AppResources\.(\w+)", RegexOptions.CultureInvariant);

    private static readonly Regex Placeholder = new(@"\{\d+(?:[,:][^}]*)?\}", RegexOptions.CultureInvariant);

    [Theory]
    [InlineData("AppResources.resx")]
    [InlineData("AppResources.es-MX.resx")]
    public void NoStringReferencedFromXamlCarriesAFormatPlaceholder(string resourceFile)
    {
        var values = XDocument.Load(Path.Combine(AppRoot(), "Resources", "Strings", resourceFile)).Root!.Elements("data")
            .ToDictionary(data => data.Attribute("name")!.Value, data => data.Element("value")?.Value ?? string.Empty);

        var hits = XamlFiles()
            .SelectMany(file => StaticReference.Matches(File.ReadAllText(file))
                .Select(match => (file, name: match.Groups[1].Value)))
            .Where(entry => values.TryGetValue(entry.name, out var value) && Placeholder.IsMatch(value))
            .Select(entry => $"{Path.GetRelativePath(AppRoot(), entry.file)}: {entry.name} = \"{values[entry.name]}\"")
            .Distinct()
            .ToList();

        Assert.True(hits.Count == 0, $"x:Static string with a format placeholder ({resourceFile}):\n" + string.Join("\n", hits));
    }

    private static IEnumerable<string> XamlFiles()
    {
        var root = AppRoot();
        return new[] { "Views", "Controls" }
            .Select(folder => Path.Combine(root, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.xaml", SearchOption.AllDirectories))
            .Append(Path.Combine(root, "AppShell.xaml"))
            .Where(File.Exists);
    }

    private static string AppRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "agenda-buddy.sln")))
            current = current.Parent;

        if (current is null)
            throw new InvalidOperationException($"Could not locate repo root walking up from {AppContext.BaseDirectory}.");

        return Path.Combine(current.FullName, "AgendaBuddy.MobileApp");
    }
}
