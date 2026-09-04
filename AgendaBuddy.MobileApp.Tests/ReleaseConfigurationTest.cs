using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests;

/// <summary>
/// Guards the shipped-build configuration that App Store review actually checks. Each of these was
/// wrong at once, and none of them fails at build time or in any other test — a Release build pointing
/// at localhost compiles perfectly and simply cannot reach a backend.
/// </summary>
public class ReleaseConfigurationTest
{
    /// <summary>
    /// Info.plist with XML comments removed, so these assertions describe the keys the OS actually reads
    /// rather than being satisfied or broken by prose. The file deliberately explains in a comment why
    /// there is no App Transport Security exception, and a naive string search would read that as one.
    /// </summary>
    private static string Plist()
    {
        var raw = File.ReadAllText(
            Path.Combine(RepositoryRoot(), "AgendaBuddy.MobileApp", "Platforms", "iOS", "Info.plist"));

        return Regex.Replace(raw, "<!--.*?-->", string.Empty, RegexOptions.Singleline);
    }

    /// <summary>
    /// App Transport Security is the guarantee that every request to the deployed gateway goes over TLS.
    /// An exception domain re-added to make a local run work would silently remove it for the shipped app.
    /// </summary>
    [Fact]
    public void InfoPlist_AllowsNoInsecureHttp()
    {
        var plist = Plist();

        Assert.DoesNotContain("NSAppTransportSecurity", plist);
        Assert.DoesNotContain("NSExceptionAllowsInsecureHTTPLoads", plist);
        Assert.DoesNotContain("NSAllowsArbitraryLoads", plist);
    }

    /// <summary>
    /// iPhone only. Declaring iPad support (UIDeviceFamily 2) makes iPad screenshots mandatory for review
    /// and puts every screen on an untested device class.
    /// </summary>
    [Fact]
    public void InfoPlist_DeclaresIPhoneOnly()
    {
        var plist = Plist();
        var families = Between(plist, "<key>UIDeviceFamily</key>", "</array>");

        Assert.Contains("<integer>1</integer>", families);
        Assert.DoesNotContain("<integer>2</integer>", families);
    }

    /// <summary>
    /// Portrait only: the UI has no landscape layout, and reviewers rotate the device.
    /// </summary>
    [Fact]
    public void InfoPlist_DeclaresPortraitOnly()
    {
        var orientations = Between(Plist(), "<key>UISupportedInterfaceOrientations</key>", "</array>");

        Assert.Contains("UIInterfaceOrientationPortrait", orientations);
        Assert.DoesNotContain("Landscape", orientations);
        Assert.DoesNotContain("UpsideDown", orientations);
    }

    /// <summary>
    /// Without this key every upload stops to ask the export-compliance question by hand.
    /// </summary>
    [Fact]
    public void InfoPlist_AnswersTheExportComplianceQuestion()
    {
        Assert.Contains("ITSAppUsesNonExemptEncryption", Plist());
    }

    /// <summary>
    /// The address a shipped build talks to. It must be a real HTTPS host: the resolver falls back to the
    /// local gateway when this is absent, which is how a Release build ended up pointing at localhost.
    /// </summary>
    [Fact]
    public void ShippedApiBaseUrl_IsAReachableHttpsAddress()
    {
        var path = Path.Combine(RepositoryRoot(), "AgendaBuddy.MobileApp", "appsettings.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));

        var value = document.RootElement.GetProperty("ApiBaseUrl").GetString();

        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.StartsWith("https://", value);
        Assert.DoesNotContain("localhost", value);
        Assert.DoesNotContain("127.0.0.1", value);
    }

    /// <summary>
    /// The Debug overlay is allowed to be local — that is its whole purpose — but it must not be the file
    /// a Release build reads.
    /// </summary>
    [Fact]
    public void DevelopmentOverlayExists_AndIsSeparateFromTheShippedFile()
    {
        var root = Path.Combine(RepositoryRoot(), "AgendaBuddy.MobileApp");

        Assert.True(File.Exists(Path.Combine(root, "appsettings.Development.json")));
        Assert.True(File.Exists(Path.Combine(root, "appsettings.json")));
    }

    /// <summary>Both files have to be embedded, or configuration silently finds nothing on device.</summary>
    [Fact]
    public void BothConfigurationFilesAreEmbeddedResources()
    {
        var csproj = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "AgendaBuddy.MobileApp", "AgendaBuddy.MobileApp.csproj"));

        Assert.Contains("""EmbeddedResource Include="appsettings.json""", csproj);
        Assert.Contains("""EmbeddedResource Include="appsettings.Development.json""", csproj);
    }

    private static string Between(string haystack, string start, string end)
    {
        var from = haystack.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"'{start}' not found in Info.plist.");

        var to = haystack.IndexOf(end, from, StringComparison.Ordinal);
        Assert.True(to >= 0, $"'{end}' not found after '{start}' in Info.plist.");

        return haystack[from..to];
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var gitPath = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath)) return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"No `.git` found above {AppContext.BaseDirectory}. This test reads Info.plist and "
            + "appsettings.json from the working tree and fails closed rather than passing vacuously.");
    }
}
