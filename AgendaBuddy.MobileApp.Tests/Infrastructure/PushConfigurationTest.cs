using System.Xml.Linq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

/// <summary>
/// The push wiring that lives in project and platform files rather than in code, and therefore compiles fine
/// when it is wrong.
/// </summary>
/// <remarks>
/// Every assertion here stands for a failure that is silent at build time and at send time. A bundle-id
/// mismatch, a missing background mode or a stray entitlements file all produce a green build, a successful
/// FCM response, and no notification — the hardest shape of bug to chase, and one this feature already hit
/// three separate times.
/// </remarks>
public class PushConfigurationTest
{
    private const string ApplicationId = "com.fererelabs.agendabuddy";

    private static string MobileApp() => Path.Combine(RepoRoot(), "AgendaBuddy.MobileApp");

    private static string Csproj() =>
        File.ReadAllText(Path.Combine(MobileApp(), "AgendaBuddy.MobileApp.csproj"));

    // ── Identity has to match on all three sides ────────────────────────────────────────────────────

    /// <summary>
    /// FCM rejects a registration whose bundle/package id does not match the app registered in the Firebase
    /// project, and the client cannot tell that apart from "no token yet".
    /// </summary>
    [Fact]
    public void TheAndroidFirebaseConfigMatchesTheApplicationId()
    {
        var json = File.ReadAllText(
            Path.Combine(MobileApp(), "Platforms", "Android", "google-services.json"));

        Assert.Contains($"\"package_name\": \"{ApplicationId}\"", json);
    }

    [Fact]
    public void TheIosFirebaseConfigMatchesTheApplicationId()
    {
        var plist = XDocument.Load(
            Path.Combine(MobileApp(), "Platforms", "iOS", "GoogleService-Info.plist"));

        Assert.Equal(ApplicationId, ValueOf(plist, "BUNDLE_ID"));
    }

    // Both config files must name the same Firebase project, or one platform silently talks to another
    // project's messaging service.
    [Fact]
    public void BothPlatformsPointAtTheSameFirebaseProject()
    {
        var json = File.ReadAllText(
            Path.Combine(MobileApp(), "Platforms", "Android", "google-services.json"));
        var plist = XDocument.Load(
            Path.Combine(MobileApp(), "Platforms", "iOS", "GoogleService-Info.plist"));

        var iosProject = ValueOf(plist, "PROJECT_ID");

        Assert.False(string.IsNullOrWhiteSpace(iosProject));
        Assert.Contains($"\"project_id\": \"{iosProject}\"", json);
    }

    [Fact]
    public void TheCsprojApplicationIdMatchesWhatTheConfigFilesExpect()
    {
        Assert.Contains($"<ApplicationId>{ApplicationId}</ApplicationId>", Csproj());
    }

    // ── The build has to consume the config files ───────────────────────────────────────────────────

    /// <summary>
    /// The Android build ignores <c>google-services.json</c> unless it is a <c>GoogleServicesJson</c> item;
    /// the iOS Firebase SDK reads <c>GoogleService-Info.plist</c> from the bundle root, so it must be a
    /// <c>BundleResource</c>. Either omission ships an app with no Firebase config.
    /// </summary>
    [Fact]
    public void EachPlatformsFirebaseConfigIsWiredIntoTheBuild()
    {
        var csproj = Csproj();

        Assert.Contains("<GoogleServicesJson Include=", csproj);
        Assert.Contains("GoogleService-Info.plist", csproj);
        Assert.Contains("<BundleResource Include=", csproj);
    }

    // Both mobile TFMs, or the platform's entire push path compiles out and nothing reports it.
    [Theory]
    [InlineData("net10.0-android")]
    [InlineData("net10.0-ios")]
    public void FirebaseIsCompiledInForBothMobileTargets(string targetFramework)
    {
        var line = Csproj()
            .Split('\n')
            .Single(l => l.Contains("FIREBASE", StringComparison.Ordinal)
                         && l.Contains("DefineConstants", StringComparison.Ordinal));

        Assert.Contains(targetFramework, line);
    }

    // ── iOS-specific delivery requirements ─────────────────────────────────────────────────────────

    /// <summary>
    /// Without <c>remote-notification</c> iOS draws the banner but never hands the payload to the app, so a
    /// tapped notification cannot open the appointment it names.
    /// </summary>
    [Fact]
    public void IosDeclaresTheRemoteNotificationBackgroundMode()
    {
        var plist = XDocument.Load(Path.Combine(MobileApp(), "Platforms", "iOS", "Info.plist"));

        var key = plist.Descendants("key").SingleOrDefault(k => k.Value == "UIBackgroundModes");
        Assert.NotNull(key);

        var modes = (XElement?)key!.NextNode;
        Assert.NotNull(modes);
        Assert.Contains("remote-notification", modes!.Elements("string").Select(e => e.Value));
    }

    /// <summary>
    /// <c>aps-environment</c> must be <c>production</c>. TestFlight and the App Store both use the production
    /// APNs environment; <c>development</c> makes APNs accept the registration and drop every message, because
    /// a token minted in one environment is not valid in the other.
    /// </summary>
    [Fact]
    public void TheApsEnvironmentIsProduction()
    {
        var plist = XDocument.Load(Path.Combine(MobileApp(), "Signing", "PushEntitlements.plist"));

        Assert.Equal("production", ValueOf(plist, "aps-environment"));
    }

    /// <summary>
    /// ⚠️ The entitlements file must NOT sit at <c>Platforms/iOS/Entitlements.plist</c>. The .NET iOS SDK
    /// auto-detects that exact path and then requires code signing for <b>every</b> build — including the
    /// unsigned Release build the "Mobile — iOS Build" CI job performs, which has no certificate and no
    /// profile. Gating <c>CodesignEntitlements</c> on <c>IosSigned</c> does not help: it is the file's presence
    /// that triggers it. This broke the build once and the failure message names signing, not entitlements,
    /// so it is worth a test rather than a comment.
    /// </summary>
    [Fact]
    public void TheEntitlementsFileIsNotAtThePathThatForcesCodeSigning()
    {
        var conventional = Path.Combine(MobileApp(), "Platforms", "iOS", "Entitlements.plist");

        Assert.False(File.Exists(conventional),
            $"{conventional} exists. The iOS SDK auto-detects this path and will require code signing for "
            + "every build, breaking the unsigned CI iOS build. Keep entitlements at "
            + "Signing/PushEntitlements.plist and reference them from the csproj.");
    }

    // The entitlement is only meaningful on a signed build, and asking an unsigned one to satisfy it is what
    // the relocation above exists to prevent.
    [Fact]
    public void EntitlementsAreOnlyAppliedToASignedBuild()
    {
        var csproj = Csproj();

        Assert.Contains("Signing\\PushEntitlements.plist", csproj);
        Assert.Contains("'$(IosSigned)' != 'true'", csproj);
    }

    // ── Android-specific delivery requirement ──────────────────────────────────────────────────────

    /// <summary>
    /// Required from Android 13. Without it the token registers and the OS drops every notification, so the
    /// server reports success and nothing appears.
    /// </summary>
    [Fact]
    public void AndroidDeclaresThePostNotificationsPermission()
    {
        var manifest = File.ReadAllText(
            Path.Combine(MobileApp(), "Platforms", "Android", "AndroidManifest.xml"));

        Assert.Contains("android.permission.POST_NOTIFICATIONS", manifest);
    }

    /// <summary>
    /// Cleartext is permitted to the loopback host only. <c>usesCleartextTraffic="true"</c> would allow
    /// plaintext to any host in the released app, which is a downgrade on a real user's traffic.
    /// </summary>
    [Fact]
    public void AndroidPermitsCleartextToTheLoopbackHostOnly()
    {
        // Parsed, not substring-matched: the manifest's own comment names usesCleartextTraffic as the thing
        // being avoided, so a text search finds it and proves nothing.
        var manifest = XDocument.Load(
            Path.Combine(MobileApp(), "Platforms", "Android", "AndroidManifest.xml"));
        var android = XNamespace.Get("http://schemas.android.com/apk/res/android");
        var application = manifest.Root!.Element("application")!;

        Assert.Null(application.Attribute(android + "usesCleartextTraffic"));
        Assert.Equal(
            "@xml/network_security_config",
            application.Attribute(android + "networkSecurityConfig")?.Value);

        var config = XDocument.Load(Path.Combine(
            MobileApp(), "Platforms", "Android", "Resources", "xml", "network_security_config.xml"));

        // Cleartext is off by default and permitted only for the named loopback domains.
        Assert.Equal("false",
            config.Root!.Element("base-config")?.Attribute("cleartextTrafficPermitted")?.Value);

        var permitted = config.Root.Elements("domain-config")
            .Where(d => d.Attribute("cleartextTrafficPermitted")?.Value == "true")
            .SelectMany(d => d.Elements("domain").Select(e => e.Value))
            .ToList();

        Assert.Contains("10.0.2.2", permitted);
        Assert.All(permitted, host =>
            Assert.True(host is "10.0.2.2" or "127.0.0.1" or "localhost",
                $"'{host}' is not a loopback host. Permitting cleartext to it would apply to the released app."));
    }

    private static string? ValueOf(XDocument plist, string key)
    {
        var element = plist.Descendants("key").SingleOrDefault(k => k.Value == key);
        return ((XElement?)element?.NextNode)?.Value;
    }

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
