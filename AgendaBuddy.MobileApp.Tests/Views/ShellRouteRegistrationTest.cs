using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Views;

/// <summary>
/// Shell navigation to an unregistered route throws at runtime, and nothing catches it at build time — a
/// typo'd destination is a page that simply never opens. Every route named in a <c>GoToAsync</c> literal is
/// checked here against what the Shell actually registers.
/// </summary>
public class ShellRouteRegistrationTest
{
    /// <summary>Matches a literal or interpolated route argument; anything computed is skipped.</summary>
    private static readonly Regex GoToAsyncLiteral = new(@"GoToAsync\(\$?""(?<route>[^""]*)""", RegexOptions.Compiled);

    private static readonly Regex RegisterRouteName = new(@"RegisterRoute\(\s*""(?<route>[^""]+)""", RegexOptions.Compiled);

    [Fact]
    public void EveryRouteNavigatedToIsRegistered()
    {
        var registered = RegisteredRoutes();
        var unknown = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var file in SourceFiles())
        {
            foreach (var match in GoToAsyncLiteral.Matches(File.ReadAllText(file)).Cast<Match>())
            {
                var route = match.Groups["route"].Value;
                var path = route.Split('?')[0];

                foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
                {
                    // ".." is relative back-navigation; a braced segment is interpolated at runtime.
                    if (segment == ".." || segment.Contains('{'))
                        continue;

                    if (!registered.Contains(segment))
                        unknown.Add($"{segment} (in {Path.GetFileName(file)})");
                }
            }
        }

        Assert.True(unknown.Count == 0,
            "These navigation targets are not registered by the Shell, so navigating to them throws at "
            + $"runtime: {string.Join(", ", unknown)}. Known routes: {string.Join(", ", registered.Order())}");
    }

    [Fact]
    public void TheAddServiceRouteIsRegistered()
    {
        Assert.Contains("addService", RegisteredRoutes());
    }

    /// <summary>
    /// Both halves of the Shell's route table: the tab/content routes declared in XAML and the pushed
    /// routes registered in code.
    /// </summary>
    private static HashSet<string> RegisteredRoutes()
    {
        var routes = new HashSet<string>(StringComparer.Ordinal);

        var shellXaml = XDocument.Load(Path.Combine(MobileAppRoot(), "AppShell.xaml"));
        foreach (var route in shellXaml.Descendants().Select(e => e.Attribute("Route")?.Value))
        {
            if (!string.IsNullOrEmpty(route))
                routes.Add(route);
        }

        var shellCode = File.ReadAllText(Path.Combine(MobileAppRoot(), "AppShell.xaml.cs"));
        foreach (var match in RegisterRouteName.Matches(shellCode).Cast<Match>())
            routes.Add(match.Groups["route"].Value);

        return routes;
    }

    private static IEnumerable<string> SourceFiles()
    {
        var root = MobileAppRoot();

        return new[] { "Views", "ViewModels", "Services", "Infrastructure", "Controls" }
            .Select(d => Path.Combine(root, d))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "*.cs", SearchOption.AllDirectories))
            .Concat(Directory.EnumerateFiles(root, "*.cs"))
            .OrderBy(f => f, StringComparer.Ordinal);
    }

    private static string MobileAppRoot() => Path.Combine(RepoRoot(), "AgendaBuddy.MobileApp");

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
