using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.ViewModels;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

/// <summary>
/// The languages the app offers, and the rule that stops it claiming one it does not speak.
/// </summary>
public class AppLanguagesTest
{
    [Fact]
    public void SpanishIsListedAsAnAvailableLanguage()
    {
        var spanish = Assert.Single(AppLanguages.All, language => language.Code == "es-MX");

        Assert.Equal("Spanish", spanish.Name);
        Assert.Equal("Español", spanish.NativeName);
        Assert.True(spanish.IsAvailable);
    }

    [Fact]
    public void SpanishIsSelectable()
    {
        Assert.True(AppLanguages.IsSelectable("es-MX"));
    }

    [Fact]
    public void EnglishIsTheAvailableDefault()
    {
        Assert.Equal("en", AppLanguages.DefaultCode);
        Assert.True(AppLanguages.IsSelectable("en"));
    }

    [Fact]
    public void BothLanguagesAreAvailable()
    {
        Assert.Equal(2, AppLanguages.All.Count(language => language.IsAvailable));
    }

    /// <summary>An unknown code is not available, so a value stored by a future build cannot be honoured here.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fr")]
    [InlineData("zz")]
    public void AnUnknownCodeIsNotSelectable(string? code)
    {
        Assert.False(AppLanguages.IsSelectable(code));
    }

    [Fact]
    public void SelectionIsCaseInsensitive()
    {
        Assert.True(AppLanguages.IsSelectable("EN"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("zz")]
    public void AnUnknownCodeResolvesToTheDefault(string? code)
    {
        Assert.Equal(AppLanguages.DefaultCode, AppLanguages.Resolve(code).Code);
    }

    [Fact]
    public async Task SelectingSpanishChangesTheSelection()
    {
        var vm = new LanguageViewModel();
        vm.LoadCommand.Execute(null);
        var spanish = vm.Languages.Single(language => language.Code == "es-MX");

        await vm.SelectCommand.ExecuteAsync(spanish);

        Assert.Equal("es-MX", vm.SelectedCode);
        Assert.True(spanish.IsSelected);
    }

    [Fact]
    public void TheAvailableLanguageOpensTicked()
    {
        var vm = new LanguageViewModel();

        vm.LoadCommand.Execute(null);

        var english = vm.Languages.Single(language => language.Code == "en");
        Assert.True(english.IsSelected);
        Assert.Single(vm.Languages, language => language.IsSelected);
    }

    [Fact]
    public void LoadingTwiceDoesNotDuplicateTheList()
    {
        var vm = new LanguageViewModel();

        vm.LoadCommand.Execute(null);
        vm.LoadCommand.Execute(null);

        Assert.Equal(AppLanguages.All.Count, vm.Languages.Count);
    }

    [Fact]
    public void SpanishRowUsesItsNativeName()
    {
        var vm = new LanguageViewModel();
        vm.LoadCommand.Execute(null);

        var spanish = vm.Languages.Single(language => language.Code == "es-MX");

        Assert.Equal("Español", spanish.Detail);
    }
}
