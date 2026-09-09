using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

public class LanguageShellLifecycleTest
{
    [Fact]
    public void AppInitializesCultureBeforeResolvingTheShell()
    {
        var source = File.ReadAllText(Path.Combine(MobileRoot(), "App.xaml.cs"));

        var initialize = source.IndexOf("InitializeLanguageAndNavigation();", StringComparison.Ordinal);
        var resolve = source.IndexOf("GetRequiredService<AppShell>", StringComparison.Ordinal);

        Assert.True(initialize >= 0 && resolve > initialize,
            "App must initialize culture before resolving AppShell, whose XAML resolves localized text.");
        Assert.Contains("_languageCoordinator.Initialize(CultureInfo.CurrentUICulture)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ReconstructableShellDoesNotRegisterGlobalRoutesOrStaticEvents()
    {
        var source = File.ReadAllText(Path.Combine(MobileRoot(), "AppShell.xaml.cs"));

        Assert.DoesNotContain("Routing.RegisterRoute", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UnauthorizedAccess +=", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellIsTransientAndGlobalNavigationIsInitializedOnce()
    {
        var source = File.ReadAllText(Path.Combine(MobileRoot(), "MauiProgram.cs"));

        Assert.Contains("AddTransient<AppShell>()", source, StringComparison.Ordinal);
        Assert.Contains("AppNavigation.RegisterRoutes()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LanguageShellRebuildRefreshesPushRegistrationWithoutWaitingForIt()
    {
        var source = File.ReadAllText(Path.Combine(MobileRoot(), "Services", "LanguageShellService.cs"));

        Assert.Contains("_ = pushNotifications.RefreshRegistrationAsync();", source, StringComparison.Ordinal);
        Assert.DoesNotContain("await pushNotifications.RefreshRegistrationAsync();", source, StringComparison.Ordinal);
    }

    private static string MobileRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "agenda-buddy.sln")))
                return Path.Combine(directory.FullName, "AgendaBuddy.MobileApp");

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repo root (agenda-buddy.sln) walking up from {AppContext.BaseDirectory}.");
    }
}