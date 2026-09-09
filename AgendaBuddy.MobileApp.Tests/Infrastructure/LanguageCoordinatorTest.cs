using System.Globalization;
using AgendaBuddy.MobileApp.Infrastructure;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

[Collection(nameof(CultureSensitiveCollection))]
public class LanguageCoordinatorTest
{
    [Fact]
    public void InitializeUsesPersistedLanguageBeforeDeviceLanguage()
    {
        var store = new FakeLanguagePreferenceStore("en");
        var coordinator = Create(store);

        coordinator.Initialize(new CultureInfo("es-MX"));

        Assert.Equal("en", coordinator.CurrentCode);
        Assert.Equal("en", CultureInfo.CurrentUICulture.Name);
    }

    [Fact]
    public void InitializeUsesSupportedSpanishDeviceLanguageOnFirstRun()
    {
        var coordinator = Create(new FakeLanguagePreferenceStore());

        coordinator.Initialize(new CultureInfo("es-ES"));

        Assert.Equal("es-MX", coordinator.CurrentCode);
        Assert.Equal("es-MX", CultureInfo.CurrentUICulture.Name);
    }

    [Fact]
    public void InitializeFallsBackToEnglishForUnsupportedDeviceLanguage()
    {
        var coordinator = Create(new FakeLanguagePreferenceStore());

        coordinator.Initialize(new CultureInfo("fr-FR"));

        Assert.Equal("en", coordinator.CurrentCode);
    }

    [Fact]
    public void SelectPersistsAndAppliesCanonicalLanguage()
    {
        var store = new FakeLanguagePreferenceStore();
        var coordinator = Create(store);

        var changed = coordinator.Select("es");

        Assert.True(changed);
        Assert.Equal("es-MX", store.Value);
        Assert.Equal("es-MX", CultureInfo.CurrentCulture.Name);
        Assert.Equal("es-MX", CultureInfo.DefaultThreadCurrentUICulture?.Name);
    }

    [Fact]
    public void SelectRefusesUnsupportedLanguage()
    {
        var store = new FakeLanguagePreferenceStore();
        var coordinator = Create(store);

        Assert.False(coordinator.Select("fr"));
        Assert.Null(store.Value);
    }

    private static LanguageCoordinator Create(FakeLanguagePreferenceStore store) =>
        new(store, code => code is "en" or "es-MX");

    private sealed class FakeLanguagePreferenceStore(string? value = null) : ILanguagePreferenceStore
    {
        public string? Value { get; private set; } = value;

        public string? Get() => Value;

        public void Set(string languageCode) => Value = languageCode;
    }
}

[CollectionDefinition(nameof(CultureSensitiveCollection), DisableParallelization = true)]
public sealed class CultureSensitiveCollection;