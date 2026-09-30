using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Resources.Strings;
using AgendaBuddy.MobileApp.Routing;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

internal static class ShowcaseFixtures
{
    public static MyShowcase Mine(int portfolio = 0, string? photo = null, string? logo = null, string? code = null) => new()
    {
        ProviderRef = "ref-1",
        PublicCode = code,
        PhotoHash = photo,
        LogoHash = logo,
        Portfolio = Enumerable.Range(1, portfolio)
            .Select(i => new PortfolioItem { Hash = $"h{i}", Caption = i == 1 ? "first" : null })
            .ToList(),
        Completeness = new ShowcaseCompleteness { Done = 2, Total = 5, Missing = ["logo", "about", "portfolio"] },
        Funnel = new ShowcaseFunnel { Scans = 3, Opened = 2, Booked = 1, WindowDays = 7 }
    };

    public static ShowcaseResult<T> Ok<T>(T value) => ShowcaseResult<T>.Ok(value);

    public static ShowcaseResult<T> Fail<T>(string code, int status = 400) => ShowcaseResult<T>.Fail(code, status);
}

public class MyShowcaseViewModelTests
{
    [Fact]
    public async Task LoadAppliesTheShowcaseAndLimitsThePreview()
    {
        var api = new Mock<IShowcaseApiService>();
        api.Setup(a => a.GetMineAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Ok(ShowcaseFixtures.Mine(portfolio: 9, photo: "p", code: "K7Q2X9")));
        var vm = new MyShowcaseViewModel(api.Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.HasShowcase);
        Assert.True(vm.HasPhoto);
        Assert.False(vm.HasLogo);
        Assert.Equal(MyShowcaseViewModel.PreviewCount, vm.Preview.Count);
        Assert.Equal("K7Q 2X9", vm.PublicCodeDisplay);
        Assert.Equal(0.4, vm.CompletenessProgress, 3);
        Assert.False(vm.IsComplete);
        Assert.False(vm.ShowPortfolioPrompt);
        Assert.False(string.IsNullOrWhiteSpace(vm.FunnelSentence));
    }

    [Fact]
    public async Task AnEmptyShowcaseShowsThePromptsNotBlanks()
    {
        var api = new Mock<IShowcaseApiService>();
        api.Setup(a => a.GetMineAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Ok(ShowcaseFixtures.Mine()));
        var vm = new MyShowcaseViewModel(api.Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.ShowPortfolioPrompt);
        Assert.False(vm.HasTagline);
        Assert.Equal(AppResources.GetString("Showcase_TaglineEmpty"), vm.TaglineText);
        Assert.Equal(AppResources.GetString("Showcase_AboutEmpty"), vm.AboutText);
    }

    [Fact]
    public async Task AFailedLoadNamesTheFailureAndShowsNoPrompt()
    {
        var api = new Mock<IShowcaseApiService>();
        api.Setup(a => a.GetMineAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Fail<MyShowcase>(ShowcaseErrorCodes.Network, 0));
        var vm = new MyShowcaseViewModel(api.Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(ShowcaseErrorCopy.For(ShowcaseErrorCodes.Network), vm.ErrorMessage);
        Assert.False(vm.ShowPortfolioPrompt);
    }

    [Fact]
    public async Task RefreshClearsItsOwnFlag()
    {
        var api = new Mock<IShowcaseApiService>();
        api.Setup(a => a.GetMineAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Ok(ShowcaseFixtures.Mine()));
        var vm = new MyShowcaseViewModel(api.Object) { IsRefreshing = true };

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.IsRefreshing);
    }
}

public class ShowcasePhotoLogoViewModelTests
{
    [Fact]
    public async Task ChoosingAPhotoUploadsThenSetsItByHash()
    {
        var api = new Mock<IShowcaseApiService>();
        var picker = new Mock<IImagePicker>();
        picker.Setup(p => p.PickAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync([new byte[] { 1, 2 }]);
        api.Setup(a => a.UploadImageAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Ok(new MediaUpload { Hash = "new-photo" }));
        api.Setup(a => a.SetPhotoAsync("new-photo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Ok(ShowcaseFixtures.Mine(photo: "new-photo")));
        var vm = new ShowcasePhotoLogoViewModel(api.Object, picker.Object);

        await vm.ChoosePhotoCommand.ExecuteAsync(null);

        Assert.Equal("new-photo", vm.PhotoHash);
        Assert.True(vm.IsIdle);
        api.Verify(a => a.SetLogoAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ACancelledPickerChangesNothing()
    {
        var api = new Mock<IShowcaseApiService>();
        var picker = new Mock<IImagePicker>();
        picker.Setup(p => p.PickAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<byte[]>());
        var vm = new ShowcasePhotoLogoViewModel(api.Object, picker.Object);

        await vm.ChooseLogoCommand.ExecuteAsync(null);

        Assert.False(vm.HasError);
        api.Verify(a => a.UploadImageAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ARejectedUploadNamesTheReasonAndSetsNothing()
    {
        var api = new Mock<IShowcaseApiService>();
        var picker = new Mock<IImagePicker>();
        picker.Setup(p => p.PickAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync([new byte[] { 1 }]);
        api.Setup(a => a.UploadImageAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Fail<MediaUpload>(ShowcaseErrorCodes.TooLarge, 413));
        var vm = new ShowcasePhotoLogoViewModel(api.Object, picker.Object);

        await vm.ChooseLogoCommand.ExecuteAsync(null);

        Assert.Equal(ShowcaseErrorCopy.For(ShowcaseErrorCodes.TooLarge), vm.ErrorMessage);
        api.Verify(a => a.SetLogoAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemovingSetsNull()
    {
        var api = new Mock<IShowcaseApiService>();
        api.Setup(a => a.SetLogoAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Ok(ShowcaseFixtures.Mine(photo: "p")));
        var vm = new ShowcasePhotoLogoViewModel(api.Object, new UnavailableImagePicker()) { LogoHash = "old" };

        await vm.RemoveLogoCommand.ExecuteAsync(null);

        Assert.False(vm.HasLogo);
        Assert.Equal("p", vm.PhotoHash);
    }

    [Fact]
    public async Task APickerThatThrowsIsReportedNotRaised()
    {
        var picker = new Mock<IImagePicker>();
        picker.Setup(p => p.PickAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());
        var vm = new ShowcasePhotoLogoViewModel(new Mock<IShowcaseApiService>().Object, picker.Object);

        await vm.ChoosePhotoCommand.ExecuteAsync(null);

        Assert.Equal(AppResources.GetString("Showcase_PickFailed"), vm.ErrorMessage);
    }
}

public class ShowcaseTextViewModelTests
{
    [Fact]
    public void SaveIsAlwaysExecutable()
    {
        var vm = new ShowcaseTextViewModel(new Mock<IShowcaseApiService>().Object)
        {
            Tagline = new string('x', ShowcaseText.TaglineMaxLength + 5)
        };

        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task ATooLongTaglineIsNamedAndNotSent()
    {
        var api = new Mock<IShowcaseApiService>();
        var vm = new ShowcaseTextViewModel(api.Object) { Tagline = new string('x', ShowcaseText.TaglineMaxLength + 1) };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(vm.IsTaglineTooLong);
        Assert.Equal(AppResources.Format("Showcase_TaglineTooLong", ShowcaseText.TaglineMaxLength), vm.ErrorMessage);
        api.Verify(a => a.SetTextAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ATooLongAboutIsNamedAndNotSent()
    {
        var api = new Mock<IShowcaseApiService>();
        var vm = new ShowcaseTextViewModel(api.Object) { About = new string('x', ShowcaseText.AboutMaxLength + 1) };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(AppResources.Format("Showcase_AboutTooLong", ShowcaseText.AboutMaxLength), vm.ErrorMessage);
    }

    [Fact]
    public async Task SaveTrimsAndSendsBlankAsNull()
    {
        var api = new Mock<IShowcaseApiService>();
        api.Setup(a => a.SetTextAsync("Fine line", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Ok(ShowcaseFixtures.Mine()));
        var vm = new ShowcaseTextViewModel(api.Object) { Tagline = "  Fine line ", About = "   " };
        var saved = false;
        vm.Saved += (_, _) => saved = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(saved);
        Assert.False(vm.HasError);
        api.Verify(a => a.SetTextAsync("Fine line", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void CountersShowUsedOverMax()
    {
        var vm = new ShowcaseTextViewModel(new Mock<IShowcaseApiService>().Object) { Tagline = "abc" };

        Assert.Equal($"3/{ShowcaseText.TaglineMaxLength}", vm.TaglineCounter);
        Assert.Equal($"0/{ShowcaseText.AboutMaxLength}", vm.AboutCounter);
    }

    [Fact]
    public async Task AFailedSaveDoesNotRaiseSaved()
    {
        var api = new Mock<IShowcaseApiService>();
        api.Setup(a => a.SetTextAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Fail<MyShowcase>(ShowcaseErrorCodes.Network, 0));
        var vm = new ShowcaseTextViewModel(api.Object) { Tagline = "ok" };
        var saved = false;
        vm.Saved += (_, _) => saved = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(saved);
        Assert.True(vm.HasError);
    }
}

public class PortfolioViewerViewModelTests
{
    [Fact]
    public void OpensOnTheTappedTileAndClampsOutOfRange()
    {
        var tiles = PortfolioTile.From("r", ShowcaseFixtures.Mine(portfolio: 3).Portfolio);
        var vm = new PortfolioViewerViewModel();

        vm.Open(tiles, 0);
        Assert.Equal(AppResources.Format("Showcase_PortfolioCount", 1, 3), vm.PositionLabel);
        Assert.True(vm.HasCaption);

        vm.Open(tiles, 9);
        Assert.Equal(2, vm.Position);
        Assert.False(vm.HasCaption);
    }

    [Fact]
    public void AnEmptyViewerHasNoLabel()
    {
        var vm = new PortfolioViewerViewModel();

        vm.Open([], 3);

        Assert.Equal(string.Empty, vm.PositionLabel);
        Assert.Equal(string.Empty, vm.Caption);
    }
}

public class HiddenProvidersViewModelTests
{
    [Fact]
    public async Task LoadListsHiddenProvidersByName()
    {
        var api = new Mock<IShowcaseApiService>();
        api.Setup(a => a.GetHiddenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ShowcaseFixtures.Ok(new List<HiddenProvider>
        {
            new() { ProviderRef = "b", FirstName = "Zoe" },
            new() { ProviderRef = "a", FirstName = "Ana" }
        }));
        var vm = new HiddenProvidersViewModel(api.Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(["Ana", "Zoe"], vm.Providers.Select(p => p.FirstName));
        Assert.False(vm.IsEmpty);
    }

    [Fact]
    public async Task AFailedLoadIsNotTheEmptyState()
    {
        var api = new Mock<IShowcaseApiService>();
        api.Setup(a => a.GetHiddenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Fail<List<HiddenProvider>>(ShowcaseErrorCodes.Network, 0));
        var vm = new HiddenProvidersViewModel(api.Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.False(vm.IsEmpty);
    }

    [Fact]
    public async Task UnhideRemovesTheRowOnlyWhenTheServerAgrees()
    {
        var api = new Mock<IShowcaseApiService>();
        var ana = new HiddenProvider { ProviderRef = "a", FirstName = "Ana" };
        var zoe = new HiddenProvider { ProviderRef = "b", FirstName = "Zoe" };
        api.Setup(a => a.GetHiddenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Ok(new List<HiddenProvider> { ana, zoe }));
        api.Setup(a => a.UnhideAsync("a", It.IsAny<CancellationToken>())).ReturnsAsync(ShowcaseFixtures.Ok(true));
        api.Setup(a => a.UnhideAsync("b", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Fail<bool>(ShowcaseErrorCodes.Network, 0));
        var vm = new HiddenProvidersViewModel(api.Object);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.UnhideCommand.ExecuteAsync(ana);
        await vm.UnhideCommand.ExecuteAsync(zoe);

        Assert.Equal([zoe], vm.Providers);
        Assert.True(vm.HasError);
    }
}

public class ShowcaseLinkViewModelTests
{
    [Fact]
    public async Task LoadResolvesThroughLookupAndNeverOpensTheShowcase()
    {
        var api = new Mock<IShowcaseApiService>();
        api.Setup(a => a.LookupAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Ok(new List<ShowcaseLookupEntry>
            {
                new() { Email = "ana@x.dev", ProviderRef = "ref-a", PhotoHash = "ph" }
            }));
        var vm = new ShowcaseLinkViewModel(api.Object);

        await vm.LoadAsync("ANA@x.dev", "Ana Ruiz");

        Assert.True(vm.HasShowcase);
        Assert.Equal("ref-a", vm.ProviderRef);
        Assert.True(vm.HasPhoto);
        Assert.Equal(ShowcaseText.SeeWork("Ana"), vm.SeeWorkLabel);
        api.Verify(a => a.GetShowcaseAsync(It.IsAny<string>(), It.IsAny<ShowcaseSource>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnAddressTheLookupOmitsLeavesNoLink()
    {
        var api = new Mock<IShowcaseApiService>();
        api.Setup(a => a.LookupAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Ok(new List<ShowcaseLookupEntry>()));
        var vm = new ShowcaseLinkViewModel(api.Object);

        await vm.LoadAsync("stranger@x.dev", "Someone");

        Assert.False(vm.HasShowcase);
    }

    [Fact]
    public async Task APresetLinkIsNotLookedUpAgain()
    {
        var api = new Mock<IShowcaseApiService>();
        var vm = new ShowcaseLinkViewModel(api.Object);
        vm.Preset("ana@x.dev", "Ana Ruiz", "ref-a", null);

        await vm.LoadAsync("ana@x.dev", "Ana Ruiz");

        Assert.True(vm.HasShowcase);
        api.Verify(a => a.LookupAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
