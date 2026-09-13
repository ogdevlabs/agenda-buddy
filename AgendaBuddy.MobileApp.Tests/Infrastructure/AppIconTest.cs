using System.Xml.Linq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

public class AppIconTest
{
    [Fact]
    public void AppIconAndSplashUseTheAgendaMeMarkAndPalette()
    {
        var root = RepoRoot();
        var foreground = XDocument.Load(Path.Combine(
            root, "AgendaBuddy.MobileApp", "Resources", "AppIcon", "appiconfg.svg"));
        var splash = XDocument.Load(Path.Combine(
            root, "AgendaBuddy.MobileApp", "Resources", "Splash", "splash.svg"));
        var project = File.ReadAllText(Path.Combine(
            root, "AgendaBuddy.MobileApp", "AgendaBuddy.MobileApp.csproj"));

        Assert.DoesNotContain(foreground.Descendants(), element => element.Name.LocalName == "text");
        Assert.DoesNotContain(splash.Descendants(), element => element.Name.LocalName == "text");
        Assert.Contains(foreground.Descendants(), element =>
            element.Name.LocalName == "path"
            && (string?)element.Attribute("stroke") == "#087F6A");
        Assert.Contains(foreground.Descendants(), element =>
            element.Name.LocalName == "circle"
            && (string?)element.Attribute("fill") == "#F3C969");
        Assert.Contains("<MauiIcon", project, StringComparison.Ordinal);
        Assert.Contains("<MauiSplashScreen", project, StringComparison.Ordinal);
        Assert.Equal(2, project.Split("Color=\"#075E54\"", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("#512BD4", project, StringComparison.OrdinalIgnoreCase);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "agenda-buddy.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
