using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.ViewModels;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

/// <summary>
/// The languages the app offers, and the rule that stops it claiming one it does not speak.
/// </summary>
public class AppLanguagesTest
{
    /// <summary>
    /// Spanish is listed and marked unavailable rather than omitted: it is the language this product's market will
    /// look for first, and a screen listing only English answers "is Spanish coming?" with silence.
    /// </summary>
    [Fact]
    public void SpanishIsListedAsATargetLanguage()
    {
        var spanish = Assert.Single(AppLanguages.All.Where(language => language.Code == "es"));

        Assert.Equal("Spanish", spanish.Name);
        Assert.Equal("Español", spanish.NativeName);
    }

    /// <summary>
    /// ⚠️ <b>Spanish must stay unavailable until something actually localises the app.</b> There is no
    /// resource-string infrastructure here — every user-facing string is a literal in XAML or a view model — so
    /// flipping this flag would make the app offer a language it renders in English. This test fails the moment
    /// somebody flips it without doing the work.
    /// </summary>
    [Fact]
    public void SpanishIsNotYetSelectable()
    {
        Assert.False(AppLanguages.IsSelectable("es"));
    }

    [Fact]
    public void EnglishIsTheAvailableDefault()
    {
        Assert.Equal("en", AppLanguages.DefaultCode);
        Assert.True(AppLanguages.IsSelectable("en"));
    }

    [Fact]
    public void ExactlyOneLanguageIsAvailable()
    {
        Assert.Single(AppLanguages.All.Where(language => language.IsAvailable));
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

    /// <summary>
    /// ⚠️ <b>An unavailable language is refused, not stored.</b> Recording a preference nothing honours is worse
    /// than refusing it — the user would see Spanish ticked and an English app, and conclude the app is broken
    /// rather than that the language is not ready.
    /// </summary>
    [Fact]
    public async Task SelectingAnUnavailableLanguageDoesNotChangeTheSelection()
    {
        var vm = new LanguageViewModel();
        vm.LoadCommand.Execute(null);
        var spanish = vm.Languages.Single(language => language.Code == "es");

        await vm.SelectCommand.ExecuteAsync(spanish);

        Assert.Equal("en", vm.SelectedCode);
        Assert.False(spanish.IsSelected);
    }

    [Fact]
    public void TheAvailableLanguageOpensTicked()
    {
        var vm = new LanguageViewModel();

        vm.LoadCommand.Execute(null);

        var english = vm.Languages.Single(language => language.Code == "en");
        Assert.True(english.IsSelected);
        Assert.Single(vm.Languages.Where(language => language.IsSelected));
    }

    [Fact]
    public void LoadingTwiceDoesNotDuplicateTheList()
    {
        var vm = new LanguageViewModel();

        vm.LoadCommand.Execute(null);
        vm.LoadCommand.Execute(null);

        Assert.Equal(AppLanguages.All.Count, vm.Languages.Count);
    }

    /// <summary>An unavailable row has to say why, or it just looks like a row that does not work.</summary>
    [Fact]
    public void AnUnavailableRowSaysItIsComingRatherThanJustFailingToRespond()
    {
        var vm = new LanguageViewModel();
        vm.LoadCommand.Execute(null);

        var spanish = vm.Languages.Single(language => language.Code == "es");

        Assert.Contains("coming soon", spanish.Detail, StringComparison.OrdinalIgnoreCase);
    }
}
