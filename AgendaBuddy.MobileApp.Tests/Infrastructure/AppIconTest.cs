using System.Xml.Linq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

public class AppIconTest
{
    [Fact]
    public void AppIconAndSplashUseTheAgendaMeMarkAndPalette()
    {
        var root = RepoRoot();
        var markPath = Path.Combine(
            root, "AgendaBuddy.MobileApp", "Resources", "Images", "brand_mark.svg");
        var foreground = XDocument.Load(markPath);
        var splash = XDocument.Load(Path.Combine(
            root, "AgendaBuddy.MobileApp", "Resources", "Splash", "splash.svg"));
        var project = File.ReadAllText(Path.Combine(
            root, "AgendaBuddy.MobileApp", "AgendaBuddy.MobileApp.csproj"));

        Assert.DoesNotContain(foreground.Descendants(), element => element.Name.LocalName == "text");
        Assert.DoesNotContain(splash.Descendants(), element => element.Name.LocalName == "text");
        Assert.Contains(foreground.Descendants(), element =>
            element.Name.LocalName == "path"
            && (string?)element.Attribute("fill") == "#087F6A");
        Assert.Contains(foreground.Descendants(), element =>
            element.Name.LocalName == "rect"
            && (string?)element.Attribute("fill") == "#E7F5F1");
        Assert.Contains("<MauiIcon", project, StringComparison.Ordinal);
        Assert.Contains("ForegroundFile=\"Resources\\Images\\brand_mark.svg\"", project, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            root, "AgendaBuddy.MobileApp", "Resources", "AppIcon", "appiconfg.svg")));
        foreach (var view in new[] { "LoginPage.xaml", "RegisterPage.xaml", "EmailVerificationPage.xaml" })
        {
            var xaml = File.ReadAllText(Path.Combine(root, "AgendaBuddy.MobileApp", "Views", view));
            Assert.Contains("Source=\"brand_mark.png\"", xaml, StringComparison.Ordinal);
        }
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
