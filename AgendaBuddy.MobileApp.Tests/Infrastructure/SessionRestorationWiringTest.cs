using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

public class SessionRestorationWiringTest
{
    [Fact]
    public void AppRestoresPersistedSessionBeforeNavigatingToDashboard()
    {
        var source = File.ReadAllText(Path.Combine(MobileRoot(), "App.xaml.cs"));

        Assert.Contains("RestoreSessionAsync", source, StringComparison.Ordinal);
        Assert.Contains("UpdateForRoleAsync", source, StringComparison.Ordinal);
        Assert.Contains("GoToAsync(\"//dashboard\")", source, StringComparison.Ordinal);
        Assert.Equal(1, source.Split("window.Created +=", StringSplitOptions.None).Length - 1);
        Assert.Contains("if (await RestoreStoredSessionAsync())", source, StringComparison.Ordinal);
    }

    private static string MobileRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "agenda-buddy.sln")))
            current = current.Parent;

        return Path.Combine(
            current?.FullName ?? throw new InvalidOperationException("Could not locate repository root."),
            "AgendaBuddy.MobileApp");
    }
}
