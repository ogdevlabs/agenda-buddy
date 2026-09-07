using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

/// <summary>
/// The launch-time developer sign-in must exist only in a Debug build.
/// </summary>
/// <remarks>
/// <para>
/// It is not an auth bypass — it performs the ordinary <c>POST /api/v1/auth/login</c> with credentials handed to
/// the process, so the server still authenticates and a wrong password still fails. But a convenience that reads
/// credentials from the environment has no business in a shipped app at all, and <c>#if DEBUG</c> is exactly the
/// kind of guard that gets lost in a refactor without anything failing.
/// </para>
/// <para>
/// Asserted by reading the source, because this test assembly compiles against the <c>net10.0</c> slice where
/// <c>App.xaml.cs</c> (guarded by <c>#if MOBILE</c>) is not built at all — so there is no type to reflect over.
/// </para>
/// </remarks>
public class DevSignInIsDebugOnlyTest
{
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "agenda-buddy.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory!.FullName;
    }

    private static string AppSource() =>
        File.ReadAllText(Path.Combine(RepoRoot(), "AgendaBuddy.MobileApp", "App.xaml.cs"));

    /// <summary>
    /// Every reference to the dev-sign-in environment variables sits inside a <c>#if DEBUG</c> region.
    /// </summary>
    /// <remarks>
    /// Walks the conditional-compilation nesting rather than merely checking a <c>#if DEBUG</c> exists somewhere
    /// in the file — the file has more than one such region, and "present" is not the same as "containing this".
    /// </remarks>
    [Theory]
    [InlineData("MAUI_DEV_EMAIL")]
    [InlineData("MAUI_DEV_PASSWORD")]
    [InlineData("MAUI_DEV_ROUTE")]
    [InlineData("MAUI_DEV_TAB")]
    [InlineData("SignInFromLaunchEnvironmentAsync")]
    public void TheDevSignInOnlyExistsUnderIfDebug(string token)
    {
        var inDebug = false;
        var found = false;

        foreach (var raw in AppSource().Split('\n'))
        {
            var line = raw.Trim();

            if (line.StartsWith("#if DEBUG", StringComparison.Ordinal)) inDebug = true;
            else if (line.StartsWith("#else", StringComparison.Ordinal)) inDebug = false;
            else if (line.StartsWith("#endif", StringComparison.Ordinal)) inDebug = false;
            else if (line.Contains(token, StringComparison.Ordinal))
            {
                found = true;
                Assert.True(
                    inDebug,
                    $"'{token}' appears outside a #if DEBUG region in App.xaml.cs. The launch-time developer "
                    + "sign-in reads credentials from the environment and must not be compiled into a shipped "
                    + "app.");
            }
        }

        Assert.True(found, $"'{token}' was not found in App.xaml.cs — did the dev sign-in move?");
    }

    /// <summary>
    /// It signs in through <see cref="MobileApp.Services.IAuthService"/> rather than writing a token itself.
    /// </summary>
    /// <remarks>
    /// This is what keeps it honest: the server issues the token, so the shortcut is to the typing rather than to
    /// the authentication. A future version that stashed a JWT directly would be a real bypass, and would pass
    /// every other assertion here.
    /// </remarks>
    [Fact]
    public void ItAuthenticatesThroughTheRealLoginPath()
    {
        var source = AppSource();

        Assert.Contains("_authService.LoginAsync(", source);
        Assert.DoesNotContain("JwtKey", source);
        Assert.DoesNotContain("SecureStorage", source);
    }

    /// <summary>
    /// Absent credentials mean it does nothing — so a Debug build launched normally behaves exactly as before.
    /// </summary>
    [Fact]
    public void ItDoesNothingWithoutBothCredentials()
    {
        Assert.Contains(
            "if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;",
            AppSource());
    }
}
