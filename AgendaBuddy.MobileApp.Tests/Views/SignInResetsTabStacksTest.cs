using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Views;

/// <summary>
/// Signing out navigates to <c>//login</c>, which keeps every tab's pushed pages. The next account to sign in on
/// the device then opened More onto the previous account's Profile — their email, avatar and provider rows. Every
/// sign-in path must go through <c>UpdateForRoleAsync</c>, and that must pop every tab to its root first.
/// </summary>
public class SignInResetsTabStacksTest
{
    [Fact]
    public void RoleUpdatePopsEveryTabBeforeTheNextAccountIsShown()
    {
        var shell = File.ReadAllText(Path.Combine(MobileApp(), "AppShell.xaml.cs"));
        var update = shell[shell.IndexOf("Task UpdateForRoleAsync()", StringComparison.Ordinal)..];

        Assert.Contains("PopToRootAsync", shell, StringComparison.Ordinal);
        Assert.True(
            update.IndexOf("PopEveryTabToRootAsync()", StringComparison.Ordinal)
            < update.IndexOf("_session.RefreshAsync()", StringComparison.Ordinal),
            "UpdateForRoleAsync must clear the previous account's pages before it loads the new session.");
    }

    [Theory]
    [InlineData("Views/LoginPage.xaml.cs")]
    [InlineData("App.xaml.cs")]
    public void EverySignInPathUpdatesTheShellForTheNewAccount(string file)
    {
        var source = File.ReadAllText(Path.Combine(MobileApp(), file));

        Assert.Contains("UpdateForRoleAsync()", source, StringComparison.Ordinal);
    }

    private static string MobileApp()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "agenda-buddy.sln")))
                return Path.Combine(directory.FullName, "AgendaBuddy.MobileApp");

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate repo root (agenda-buddy.sln) walking up from {AppContext.BaseDirectory}.");
    }
}
