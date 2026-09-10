using System.Xml.Linq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

public class EmailVerificationAppLinkConfigurationTest
{
    private static string RepoRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void AndroidAppLinksDefaultOffAndCompileOnlyWhenEnabled()
    {
        var project = XDocument.Load(Path.Combine(
            RepoRoot(), "AgendaBuddy.MobileApp", "AgendaBuddy.MobileApp.csproj"));
        var defaultProperty = project.Descendants("AndroidEmailVerificationAppLinksEnabled")
            .Single(element => element.Attribute("Condition")?.Value.Contains("== ''", StringComparison.Ordinal) == true);
        var define = project.Descendants("DefineConstants")
            .Single(element => element.Value.Contains("ANDROID_EMAIL_VERIFICATION_APP_LINKS", StringComparison.Ordinal));

        Assert.Equal("false", defaultProperty.Value);
        Assert.Contains("'$(AndroidEmailVerificationAppLinksEnabled)' == 'true'", define.Attribute("Condition")?.Value);

        var activity = File.ReadAllText(Path.Combine(
            RepoRoot(), "AgendaBuddy.MobileApp", "Platforms", "Android", "MainActivity.cs"));
        Assert.Contains("#if ANDROID_EMAIL_VERIFICATION_APP_LINKS", activity);
        Assert.Contains("DataHost = \"agendame.app\"", activity);
        Assert.Contains("AutoVerify = true", activity);
    }

    [Fact]
    public void AndroidAssociationFileRemainsATodoTemplateWhileFlagIsOff()
    {
        var directory = Path.Combine(RepoRoot(), "infra", "app-links");

        Assert.False(File.Exists(Path.Combine(directory, "assetlinks.json")));
        Assert.Contains(
            "TODO_GOOGLE_PLAY_APP_SIGNING_SHA256",
            File.ReadAllText(Path.Combine(directory, "assetlinks.todo.json")));
    }

    [Fact]
    public void IosUniversalLinksRemainEnabled()
    {
        var entitlements = XDocument.Load(Path.Combine(
            RepoRoot(), "AgendaBuddy.MobileApp", "Signing", "PushEntitlements.plist"));

        Assert.Contains(
            entitlements.Descendants("string"),
            element => element.Value == "applinks:agendame.app");
    }
}