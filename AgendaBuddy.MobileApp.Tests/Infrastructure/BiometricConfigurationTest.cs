using System.Xml.Linq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

public class BiometricConfigurationTest
{
    [Fact]
    public void AndroidUsesTheSystemBiometricPromptAndDeclaresPermission()
    {
        var manifest = File.ReadAllText(Path.Combine(MobileRoot(), "Platforms", "Android", "AndroidManifest.xml"));
        var source = File.ReadAllText(Path.Combine(
            MobileRoot(), "Platforms", "Android", "AndroidBiometricAuthenticationService.cs"));
        var project = File.ReadAllText(Path.Combine(MobileRoot(), "AgendaBuddy.MobileApp.csproj"));

        Assert.Contains("android.permission.USE_BIOMETRIC", manifest);
        Assert.Contains("Android.Hardware.Biometrics", source);
        Assert.Contains("BiometricPrompt", source);
        Assert.Contains(">29.0</SupportedOSPlatformVersion>", project);
        Assert.DoesNotContain("Xamarin.AndroidX.Biometric", project);
    }

    [Fact]
    public void IosUsesLocalAuthenticationAndExplainsFaceIdUsage()
    {
        var source = File.ReadAllText(Path.Combine(
            MobileRoot(), "Platforms", "iOS", "IosBiometricAuthenticationService.cs"));
        var plist = XDocument.Load(Path.Combine(MobileRoot(), "Platforms", "iOS", "Info.plist"));

        Assert.Contains("LocalAuthentication", source);
        Assert.Contains("DeviceOwnerAuthenticationWithBiometrics", source);
        Assert.Equal(
            "Use Face ID to securely unlock your AgendaMe session.",
            PlistValue(plist, "NSFaceIDUsageDescription"));
    }

    [Fact]
    public void DependencyInjectionSelectsEachPlatformImplementation()
    {
        var source = File.ReadAllText(Path.Combine(MobileRoot(), "MauiProgram.cs"));

        Assert.Contains("IBiometricAuthenticationService, AndroidBiometricAuthenticationService", source);
        Assert.Contains("IBiometricAuthenticationService, IosBiometricAuthenticationService", source);
        Assert.Contains("IBiometricAuthenticationService, UnavailableBiometricAuthenticationService", source);
    }

    private static string PlistValue(XDocument plist, string key)
    {
        var keyElement = plist.Descendants("key").Single(element => element.Value == key);
        return ((XElement?)keyElement.NextNode)?.Value
               ?? throw new InvalidOperationException($"{key} has no value.");
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
