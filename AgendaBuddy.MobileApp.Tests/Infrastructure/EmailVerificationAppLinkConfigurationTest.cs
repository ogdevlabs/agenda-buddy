using System.Xml.Linq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

public class EmailVerificationAppLinkConfigurationTest
{
    private static string RepoRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void AndroidRegistersTheInstalledAppSchemeWithoutADomainFilter()
    {
        var activity = File.ReadAllText(Path.Combine(
            RepoRoot(), "AgendaBuddy.MobileApp", "Platforms", "Android", "MainActivity.cs"));
        Assert.Contains("DataScheme = \"agendame\"", activity);
        Assert.DoesNotContain("agendame.app", activity, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AutoVerify", activity, StringComparison.Ordinal);
    }

    [Fact]
    public void IosRegistersTheInstalledAppSchemeWithoutAssociatedDomains()
    {
        var info = XDocument.Load(Path.Combine(
            RepoRoot(), "AgendaBuddy.MobileApp", "Platforms", "iOS", "Info.plist"));
        var entitlements = File.ReadAllText(Path.Combine(
            RepoRoot(), "AgendaBuddy.MobileApp", "Signing", "PushEntitlements.plist"));

        Assert.Contains(info.Descendants("string"), element => element.Value == "agendame");
        Assert.DoesNotContain("associated-domains", entitlements, StringComparison.Ordinal);
        Assert.DoesNotContain("agendame.app", entitlements, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoEmailVerificationAssociationFilesAreShipped()
    {
        var directory = Path.Combine(RepoRoot(), "infra", "app-links");

        Assert.False(File.Exists(Path.Combine(directory, "assetlinks.json")));
        Assert.False(File.Exists(Path.Combine(directory, "assetlinks.todo.json")));
        Assert.False(File.Exists(Path.Combine(directory, "apple-app-site-association")));
    }
}