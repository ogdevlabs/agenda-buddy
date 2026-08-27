using AgendaBuddy.MobileApp.ViewModels;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

/// <summary>
/// SeedDataProvider must be unreachable from any ViewModel. The type itself no
/// longer exists in the AgendaBuddy.MobileApp assembly (Services/SeedDataProvider.cs was deleted), which is a
/// stronger guarantee than a per-ViewModel check — if any ViewModel (or anything else) still
/// referenced it, the assembly containing this very test would fail to build in the first place.
/// </summary>
public class SeedDataProviderRemovalTests
{
    [Fact]
    public void SeedDataProviderType_NoLongerExistsInMobileAppAssembly()
    {
        var mobileAppAssembly = typeof(DashboardViewModel).Assembly;

        var seedType = mobileAppAssembly.GetType("AgendaBuddy.MobileApp.Services.SeedDataProvider");

        Assert.Null(seedType);
    }

    [Fact]
    public void MobileAppAssembly_ContainsNoTypeNamedSeedDataProvider()
    {
        var mobileAppAssembly = typeof(DashboardViewModel).Assembly;

        Assert.DoesNotContain(
            mobileAppAssembly.GetTypes(),
            t => t.Name == "SeedDataProvider");
    }
}
