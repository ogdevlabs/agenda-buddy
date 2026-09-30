using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Resources.Strings;
using AgendaBuddy.MobileApp.Routing;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

public class ShowcaseShareViewModelTests
{
    private readonly Mock<IShowcaseApiService> _api = new();
    private readonly Mock<IShareImageService> _share = new();
    private readonly Mock<IProviderApiService> _providers = new();
    private readonly Mock<IUserSessionService> _session = new();

    private ShowcaseShareViewModel Create(string url = "https://api.agendame.app/api/v1/go/K7Q2X9")
    {
        _session.Setup(s => s.Email).Returns("ana@x.dev");
        _providers.Setup(p => p.GetProfileAsync("ana@x.dev", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileInfo { FirstName = "Ana", LastName = "Ruiz" });
        _api.Setup(a => a.GetPublicCodeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Ok(new ShowcasePublicCode { Code = "K7Q2X9", Url = url }));
        return new ShowcaseShareViewModel(_api.Object, _share.Object, _providers.Object, _session.Object);
    }

    [Fact]
    public async Task LoadShowsTheGroupedCodeAndAQrOfTheLink()
    {
        var vm = Create();

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("K7Q 2X9", vm.DisplayCode);
        Assert.True(vm.CanShare);
        Assert.NotNull(vm.QrPng);
        Assert.Equal("Ana Ruiz", vm.ProviderName);
        Assert.True(ShowcaseCodeParser.TryParseScan(vm.QrPayload, out var scanned));
        Assert.Equal("K7Q2X9", scanned);
    }

    [Fact]
    public async Task WithoutALinkThereIsNoQrRatherThanAnUnscannableOne()
    {
        var vm = Create(url: "");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Null(vm.QrPng);
    }

    [Theory]
    [InlineData(ShareFormat.Story)]
    [InlineData(ShareFormat.Square)]
    [InlineData(ShareFormat.Card)]
    public async Task EachFormatSharesTheTwoStepText(ShareFormat format)
    {
        var vm = Create();
        await vm.LoadCommand.ExecuteAsync(null);
        ShareCardText? sent = null;
        _share.Setup(s => s.ShareAsync(format, It.IsAny<ShareCardText>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<ShareFormat, ShareCardText, string, CancellationToken>((_, text, _, _) => sent = text)
            .ReturnsAsync(ShareOutcome.Shared);

        await vm.ShareAsync(format);

        Assert.NotNull(sent);
        Assert.Equal("K7Q 2X9", sent!.Code);
        Assert.Contains("K7Q 2X9", sent.StepTwo);
        Assert.False(vm.HasError);
        Assert.False(vm.IsPreparing);
    }

    [Fact]
    public async Task ACancelledShareIsNotAnError()
    {
        var vm = Create();
        await vm.LoadCommand.ExecuteAsync(null);
        _share.Setup(s => s.ShareAsync(It.IsAny<ShareFormat>(), It.IsAny<ShareCardText>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShareOutcome.Cancelled);

        await vm.ShareStoryCommand.ExecuteAsync(null);

        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task AFailedOrThrowingShareSaysTheImageFailed()
    {
        var vm = Create();
        await vm.LoadCommand.ExecuteAsync(null);
        _share.Setup(s => s.ShareAsync(It.IsAny<ShareFormat>(), It.IsAny<ShareCardText>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException());

        await vm.ShareCardCommand.ExecuteAsync(null);

        Assert.Equal(AppResources.GetString("Share_ImageFailed"), vm.ErrorMessage);
        Assert.False(vm.IsPreparing);
    }

    [Fact]
    public async Task NothingIsSharedBeforeACodeExists()
    {
        var vm = new ShowcaseShareViewModel(_api.Object, _share.Object, _providers.Object, _session.Object);

        await vm.ShareAsync(ShareFormat.Square);

        _share.Verify(s => s.ShareAsync(It.IsAny<ShareFormat>(), It.IsAny<ShareCardText>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

public class ScanProviderViewModelTests
{
    private const string Link = "https://api.agendame.app/api/v1/go/K7Q2X9";

    private readonly Mock<IShowcaseApiService> _api = new();
    private readonly Mock<IQrScanner> _scanner = new();

    private ScanProviderViewModel Create(CameraAccess access = CameraAccess.Granted)
    {
        _scanner.Setup(s => s.EnsureCameraAccessAsync()).ReturnsAsync(access);
        return new ScanProviderViewModel(_api.Object, _scanner.Object);
    }

    [Fact]
    public async Task AGrantedCameraStartsDetecting()
    {
        var vm = Create();

        await vm.StartCommand.ExecuteAsync(null);

        Assert.True(vm.IsCameraReady);
        Assert.True(vm.IsDetecting);
        Assert.False(vm.IsTyping);
    }

    [Theory]
    [InlineData(CameraAccess.Denied)]
    [InlineData(CameraAccess.Unavailable)]
    public async Task WithoutACameraTypingIsOffered(CameraAccess access)
    {
        var vm = Create(access);

        await vm.StartCommand.ExecuteAsync(null);

        Assert.True(vm.IsTyping);
        Assert.False(vm.IsDetecting);
        Assert.Equal(access == CameraAccess.Denied, vm.IsCameraDenied);
    }

    [Fact]
    public void OpenSettingsAsksTheScanner()
    {
        var vm = Create(CameraAccess.Denied);

        vm.OpenSettingsCommand.Execute(null);

        _scanner.Verify(s => s.OpenAppSettings(), Times.Once);
    }

    [Fact]
    public async Task AForeignQrSaysSoOnceAndOpensNothing()
    {
        var vm = Create();
        await vm.StartCommand.ExecuteAsync(null);

        await vm.HandleScannedAsync("https://example.com/menu");
        vm.ScanMessage = string.Empty;
        await vm.HandleScannedAsync("https://example.com/menu");

        Assert.False(vm.HasScanMessage);
        _api.Verify(a => a.GetShowcaseByCodeAsync(It.IsAny<string>(), It.IsAny<ShowcaseSource>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AForeignQrShowsTheNotOursMessage()
    {
        var vm = Create();

        await vm.HandleScannedAsync("WIFI:S:home;T:WPA;P:secret;;");

        Assert.Equal(AppResources.GetString("Scan_NotAnAgendaMeCode"), vm.ScanMessage);
    }

    [Fact]
    public async Task AScannedLinkResolvesWithTheScanSource()
    {
        var vm = Create();
        var view = new ShowcaseView { ProviderRef = "ref-a", FirstName = "Ana" };
        _api.Setup(a => a.GetShowcaseByCodeAsync("K7Q2X9", ShowcaseSource.Scan, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Ok(view));
        ShowcaseResolvedEventArgs? resolved = null;
        vm.ShowcaseResolved += (_, e) => resolved = e;

        await vm.HandleScannedAsync(Link);

        Assert.NotNull(resolved);
        Assert.Same(view, resolved!.View);
        Assert.Equal(ShowcaseSource.Scan, resolved.Source);
    }

    [Fact]
    public async Task ATypedCodeResolvesWithTheCodeSource()
    {
        var vm = Create();
        _api.Setup(a => a.GetShowcaseByCodeAsync("K7Q2X9", ShowcaseSource.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Ok(new ShowcaseView { ProviderRef = "ref-a" }));
        ShowcaseResolvedEventArgs? resolved = null;
        vm.ShowcaseResolved += (_, e) => resolved = e;
        vm.TypedCode = "k7q 2x9";

        await vm.SubmitCodeCommand.ExecuteAsync(null);

        Assert.Equal(ShowcaseSource.Code, resolved?.Source);
    }

    [Fact]
    public async Task AMalformedTypedCodeIsRejectedLocally()
    {
        var vm = Create();
        vm.TypedCode = "12";

        await vm.SubmitCodeCommand.ExecuteAsync(null);

        Assert.Equal(AppResources.GetString("Scan_InvalidCode"), vm.ErrorMessage);
        _api.Verify(a => a.GetShowcaseByCodeAsync(It.IsAny<string>(), It.IsAny<ShowcaseSource>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnUnknownCodeSaysNoProviderHasIt()
    {
        var vm = Create();
        _api.Setup(a => a.GetShowcaseByCodeAsync(It.IsAny<string>(), It.IsAny<ShowcaseSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Fail<ShowcaseView>(ShowcaseErrorCodes.ShowcaseNotFound, 404));
        vm.TypedCode = "K7Q2X9";

        await vm.SubmitCodeCommand.ExecuteAsync(null);

        Assert.Equal(AppResources.GetString("Scan_CodeNotFound"), vm.ErrorMessage);
    }

    [Fact]
    public async Task AFailedScanResumesDetecting()
    {
        var vm = Create();
        await vm.StartCommand.ExecuteAsync(null);
        _api.Setup(a => a.GetShowcaseByCodeAsync(It.IsAny<string>(), It.IsAny<ShowcaseSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Fail<ShowcaseView>(ShowcaseErrorCodes.RateLimited, 429));

        await vm.HandleScannedAsync(Link);

        Assert.True(vm.IsDetecting);
        Assert.Equal(ShowcaseErrorCopy.For(ShowcaseErrorCodes.RateLimited), vm.ScanMessage);
    }
}

public class ProviderShowcaseViewModelTests
{
    private readonly Mock<IShowcaseApiService> _api = new();
    private readonly Mock<IUserSessionService> _session = new();
    private readonly Mock<IProviderApiService> _providers = new();
    private readonly Mock<ICustomerApiService> _customers = new();

    private ProviderShowcaseViewModel Create(bool customer = true)
    {
        _session.Setup(s => s.Email).Returns("me@x.dev");
        _session.Setup(s => s.IsCustomer).Returns(customer);
        _session.Setup(s => s.IsProvider).Returns(!customer);
        return new ProviderShowcaseViewModel(_api.Object, _session.Object, _providers.Object, _customers.Object)
        {
            ProviderRef = "ref-a"
        };
    }

    private static ShowcaseView View(
        bool self = false, bool subscribed = false, int portfolio = 2, string? photo = "ph", string? logo = null) => new()
        {
            ProviderRef = "ref-a",
            FirstName = "Ana",
            LastName = "Ruiz",
            Professions = ["Tattoo Artist", "Illustrator"],
            Tagline = " Fine line ",
            PhotoHash = photo,
            LogoHash = logo,
            Portfolio = Enumerable.Range(1, portfolio).Select(i => new PortfolioItem { Hash = $"p{i}" }).ToList(),
            Relationship = new ShowcaseRelationship { IsSelf = self, IsSubscribed = subscribed }
        };

    private void Returns(ShowcaseView view, ShowcaseSource source = ShowcaseSource.Directory) =>
        _api.Setup(a => a.GetShowcaseAsync("ref-a", source, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Ok(view));

    [Fact]
    public async Task LoadFetchesWithTheSourceItWasOpenedFrom()
    {
        Returns(View(), ShowcaseSource.Appointment);
        var vm = Create();
        vm.Source = ShowcaseSource.Appointment;

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Ana Ruiz", vm.FullName);
        Assert.Equal("Tattoo Artist · Illustrator", vm.ProfessionsLine);
        Assert.Equal("Fine line", vm.Tagline);
        Assert.Equal(2, vm.Tiles.Count);
    }

    [Fact]
    public async Task APreloadedShowcaseIsNotFetchedAgain()
    {
        var vm = Create();
        vm.Preload(View(), ShowcaseSource.Scan);

        await vm.LoadCommand.ExecuteAsync(null);

        _api.Verify(a => a.GetShowcaseAsync(It.IsAny<string>(), It.IsAny<ShowcaseSource>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(ShowcaseSource.Scan, vm.Source);
    }

    [Fact]
    public void TheHeroIsTheCoverThenThePhotoThenNothing()
    {
        var vm = Create();

        vm.Preload(View(), ShowcaseSource.Directory);
        Assert.Equal("p1", vm.HeroHash);

        vm.Preload(View(portfolio: 0), ShowcaseSource.Directory);
        Assert.Equal("ph", vm.HeroHash);

        vm.Preload(View(portfolio: 0, photo: null), ShowcaseSource.Directory);
        Assert.False(vm.HasHeroImage);
    }

    [Fact]
    public void TheBadgeIsTheLogoThenThePhoto()
    {
        var vm = Create();

        vm.Preload(View(logo: "lg"), ShowcaseSource.Directory);
        Assert.Equal("lg", vm.BadgeHash);

        vm.Preload(View(), ShowcaseSource.Directory);
        Assert.Equal("ph", vm.BadgeHash);
    }

    [Fact]
    public async Task NotFoundAndNetworkFailureAreDistinctStates()
    {
        var vm = Create();
        _api.SetupSequence(a => a.GetShowcaseAsync("ref-a", It.IsAny<ShowcaseSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Fail<ShowcaseView>(ShowcaseErrorCodes.ShowcaseNotFound, 404))
            .ReturnsAsync(ShowcaseFixtures.Fail<ShowcaseView>(ShowcaseErrorCodes.Network, 0));

        await vm.LoadCommand.ExecuteAsync(null);
        Assert.True(vm.IsNotFound);
        Assert.False(vm.IsNetworkError);

        await vm.RetryCommand.ExecuteAsync(null);
        Assert.False(vm.IsNotFound);
        Assert.True(vm.IsNetworkError);
    }

    [Fact]
    public async Task RefreshDrivesItsOwnFlag()
    {
        Returns(View());
        var vm = Create();
        vm.IsRefreshing = true;

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.IsRefreshing);
    }

    [Fact]
    public void PreviewingYourOwnShowcaseDisablesEveryActionAndSaysWhy()
    {
        var vm = Create(customer: false);
        vm.Preload(View(self: true), ShowcaseSource.Directory);

        Assert.True(vm.IsSelf);
        Assert.False(vm.CanBook);
        Assert.False(vm.CanSubscribe);
        Assert.False(vm.CanMessage);
        Assert.False(vm.CanHide);
        Assert.False(vm.CanReport);
        Assert.Equal(AppResources.GetString("Showcase_SelfReason"), vm.ActionsReason);
    }

    [Fact]
    public void AnotherProviderCanReportButNotBookOrHide()
    {
        var vm = Create(customer: false);
        vm.Preload(View(), ShowcaseSource.Directory);

        Assert.False(vm.CanBook);
        Assert.False(vm.CanHide);
        Assert.True(vm.CanReport);
        Assert.Equal(AppResources.GetString("Showcase_CustomersOnlyReason"), vm.ActionsReason);
    }

    [Fact]
    public void MessagingOpensWithTheSubscription()
    {
        var vm = Create();

        vm.Preload(View(subscribed: false), ShowcaseSource.Directory);
        Assert.True(vm.CanBook);
        Assert.False(vm.CanMessage);
        Assert.Equal(AppResources.GetString("Showcase_SubscribeToMessage"), vm.ActionsReason);

        vm.Preload(View(subscribed: true), ShowcaseSource.Directory);
        Assert.True(vm.CanMessage);
        Assert.False(vm.HasActionsReason);
    }

    [Fact]
    public async Task BookUsesTheEmailTheCallerPassed()
    {
        var vm = Create();
        vm.ProviderEmail = "ana@x.dev";
        vm.Preload(View(), ShowcaseSource.Directory);
        BookRequestedEventArgs? request = null;
        vm.BookRequested += (_, e) => request = e;

        await vm.BookCommand.ExecuteAsync(null);

        Assert.Equal("ana@x.dev", request?.CounterpartEmail);
        Assert.Equal("Ana Ruiz", request?.CounterpartName);
        _providers.Verify(p => p.GetProvidersAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WithoutAnEmailTheDirectoryResolvesItByRef()
    {
        var vm = Create();
        vm.Preload(View(subscribed: true), ShowcaseSource.Scan);
        _providers.Setup(p => p.GetProvidersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new CustomerSummary { Email = "other@x.dev", ProviderRef = "ref-b", IsProvider = true },
            new CustomerSummary { Email = "ana@x.dev", ProviderRef = "REF-A", IsProvider = true }
        ]);
        CustomerSummary? thread = null;
        vm.MessageRequested += (_, e) => thread = e;

        await vm.MessageCommand.ExecuteAsync(null);

        Assert.Equal("ana@x.dev", thread?.Email);
    }

    [Fact]
    public async Task AnUnresolvableEmailSaysSoInsteadOfBookingNobody()
    {
        var vm = Create();
        vm.Preload(View(), ShowcaseSource.Scan);
        _providers.Setup(p => p.GetProvidersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var raised = false;
        vm.BookRequested += (_, _) => raised = true;

        await vm.BookCommand.ExecuteAsync(null);

        Assert.False(raised);
        Assert.Equal(AppResources.GetString("Showcase_ContactUnavailable"), vm.ErrorMessage);
    }

    [Fact]
    public async Task SubscribingFlipsTheStateOnlyWhenTheServerAgrees()
    {
        var vm = Create();
        vm.ProviderEmail = "ana@x.dev";
        vm.Preload(View(), ShowcaseSource.Directory);
        _customers.SetupSequence(c => c.SubscribeAsync("me@x.dev", "ana@x.dev", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .ReturnsAsync(true);

        await vm.ToggleSubscriptionCommand.ExecuteAsync(null);
        Assert.False(vm.IsSubscribed);
        Assert.Equal(AppResources.GetString("Error_Subscribe"), vm.ErrorMessage);

        await vm.ToggleSubscriptionCommand.ExecuteAsync(null);
        Assert.True(vm.IsSubscribed);
        Assert.True(vm.CanMessage);
        Assert.Equal(AppResources.GetString("Showcase_Unsubscribe"), vm.SubscribeLabel);
    }

    [Fact]
    public async Task ReportingOtherNeedsDetail()
    {
        var vm = Create();
        vm.Preload(View(), ShowcaseSource.Directory);

        var message = await vm.ReportAsync(ShowcaseReportReason.Other, "  ");

        Assert.Null(message);
        Assert.Equal(AppResources.GetString("ShowcaseReport_DetailRequired"), vm.ErrorMessage);
        _api.Verify(a => a.ReportAsync(It.IsAny<string>(), It.IsAny<ShowcaseReportReason>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TooMuchDetailIsNamed()
    {
        var vm = Create();
        vm.Preload(View(), ShowcaseSource.Directory);

        var message = await vm.ReportAsync(ShowcaseReportReason.Spam, new string('d', ShowcaseReportReasons.DetailMaxLength + 1));

        Assert.Null(message);
        Assert.Equal(AppResources.Format("ShowcaseReport_DetailTooLong", ShowcaseReportReasons.DetailMaxLength), vm.ErrorMessage);
    }

    [Fact]
    public async Task AReportThanksOrSaysItFailed()
    {
        var vm = Create();
        vm.Preload(View(), ShowcaseSource.Directory);
        _api.SetupSequence(a => a.ReportAsync("ref-a", ShowcaseReportReason.Spam, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Ok(true))
            .ReturnsAsync(ShowcaseFixtures.Fail<bool>(ShowcaseErrorCodes.Network, 0));

        Assert.Equal(AppResources.GetString("ShowcaseReport_Thanks"), await vm.ReportAsync(ShowcaseReportReason.Spam, null));
        Assert.Equal(AppResources.GetString("ShowcaseReport_Failed"), await vm.ReportAsync(ShowcaseReportReason.Spam, null));
    }

    [Fact]
    public void EveryReportReasonHasALabel()
    {
        Assert.All(Enum.GetValues<ShowcaseReportReason>(), r =>
            Assert.False(string.IsNullOrWhiteSpace(ProviderShowcaseViewModel.ReasonLabel(r))));
    }

    [Fact]
    public void TheHidePromptNamesTheProviderAndTheWayBack()
    {
        var vm = Create();
        vm.Preload(View(), ShowcaseSource.Directory);

        Assert.Equal(AppResources.Format("ShowcaseHide_Confirm", "Ana"), vm.HidePrompt);
    }

    [Fact]
    public async Task HideRaisesHiddenWithTheFirstName()
    {
        var vm = Create();
        vm.Preload(View(), ShowcaseSource.Directory);
        _api.Setup(a => a.HideAsync("ref-a", It.IsAny<CancellationToken>())).ReturnsAsync(ShowcaseFixtures.Ok(true));
        string? hidden = null;
        vm.Hidden += (_, name) => hidden = name;

        Assert.True(await vm.HideAsync());
        Assert.Equal("Ana", hidden);
    }

    [Fact]
    public async Task AProviderCannotHide()
    {
        var vm = Create(customer: false);
        vm.Preload(View(), ShowcaseSource.Directory);

        Assert.False(await vm.HideAsync());
        _api.Verify(a => a.HideAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void OpeningAPhotoPassesTheWholeSetAndTheIndex()
    {
        var vm = Create();
        vm.Preload(View(), ShowcaseSource.Directory);
        ShowcasePhotoRequestedEventArgs? request = null;
        vm.PhotoRequested += (_, e) => request = e;

        vm.OpenPhotoCommand.Execute(vm.Tiles[1]);

        Assert.Equal(1, request?.Index);
        Assert.Equal(2, request?.Tiles.Count);
    }
}
