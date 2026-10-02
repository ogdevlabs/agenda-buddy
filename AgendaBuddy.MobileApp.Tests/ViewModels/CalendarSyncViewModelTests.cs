using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Resources.Strings;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

[Collection("CultureSensitiveCollection")]
public sealed class CalendarSyncViewModelTests : IDisposable
{
    private const string Url = "https://g.test/api/v1/calendar/feed/token.ics";

    private readonly System.Globalization.CultureInfo? _originalCulture = AppResources.Culture;
    private readonly Mock<ICalendarFeedApiService> _api = new();
    private readonly FakeStorage _storage = new();
    private readonly Mock<IUserSessionService> _session = new();
    private readonly Mock<ICalendarSyncDevice> _device = new();

    public CalendarSyncViewModelTests()
    {
        AppResources.Culture = new System.Globalization.CultureInfo("en");
        _session.SetupGet(s => s.Email).Returns("Ana@Example.com");
        _device.SetupGet(d => d.PrefersAppleCalendar).Returns(true);
        _device.Setup(d => d.OpenAsync(It.IsAny<string>())).ReturnsAsync(true);
        _device.Setup(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
    }

    public void Dispose() => AppResources.Culture = _originalCulture;

    private CalendarSyncViewModel Sut() => new(_api.Object, _storage, _session.Object, _device.Object);

    private void StatusIs(bool enabled) =>
        _api.Setup(a => a.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CalendarFeedStatus(enabled, enabled ? DateTime.UtcNow : null));

    private void EnableReturns(string? url) =>
        _api.Setup(a => a.EnableAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(url is null ? null : new CalendarFeedLink(url, DateTime.UtcNow));

    [Fact]
    public async Task OffShowsOnlyTheOffState()
    {
        StatusIs(false);
        var sut = Sut();

        await sut.LoadCommand.ExecuteAsync(null);

        Assert.True(sut.IsOff);
        Assert.False(sut.IsOnWithLink);
        Assert.False(sut.IsOnWithoutLink);
    }

    [Fact]
    public async Task AFailedLoadIsAnErrorNotOff()
    {
        _api.Setup(a => a.GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync((CalendarFeedStatus?)null);
        var sut = Sut();

        await sut.LoadCommand.ExecuteAsync(null);

        Assert.True(sut.HasError);
        Assert.False(sut.IsOff);
        Assert.False(sut.IsOnWithLink);
    }

    [Fact]
    public async Task TurningOnStoresTheLinkUnderThisAccount()
    {
        StatusIs(false);
        EnableReturns(Url);
        var sut = Sut();
        await sut.LoadCommand.ExecuteAsync(null);

        await sut.TurnOnCommand.ExecuteAsync(null);

        Assert.True(sut.IsOnWithLink);
        Assert.Equal(Url, sut.FeedUrl);
        Assert.Equal(Url, _storage.Values["calendar_feed_url:ana@example.com"]);
    }

    [Fact]
    public async Task TurningOnSendsTheAppLanguage()
    {
        AppResources.Culture = new System.Globalization.CultureInfo("es-MX");
        StatusIs(false);
        EnableReturns(Url);
        var sut = Sut();

        await sut.TurnOnCommand.ExecuteAsync(null);

        _api.Verify(a => a.EnableAsync("es-MX", It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task AFailedTurnOnStoresNothing()
    {
        EnableReturns(null);
        var sut = Sut();

        await sut.TurnOnCommand.ExecuteAsync(null);

        Assert.True(sut.HasError);
        Assert.Empty(_storage.Values);
        Assert.False(sut.IsEnabled);
    }

    [Fact]
    public async Task OnWithTheLinkOnThisDevice()
    {
        StatusIs(true);
        _storage.Values["calendar_feed_url:ana@example.com"] = Url;
        var sut = Sut();

        await sut.LoadCommand.ExecuteAsync(null);

        Assert.True(sut.IsOnWithLink);
    }

    [Fact]
    public async Task OnWithoutTheLinkHereAsksForANewOne()
    {
        StatusIs(true);
        _storage.Values["calendar_feed_url:someone-else@example.com"] = Url;
        var sut = Sut();

        await sut.LoadCommand.ExecuteAsync(null);

        Assert.True(sut.IsOnWithoutLink);
        Assert.Empty(sut.FeedUrl);
    }

    [Fact]
    public async Task AStoredLinkForAFeedThatIsOffIsDiscarded()
    {
        StatusIs(false);
        _storage.Values["calendar_feed_url:ana@example.com"] = Url;
        var sut = Sut();

        await sut.LoadCommand.ExecuteAsync(null);

        Assert.True(sut.IsOff);
        Assert.Empty(_storage.Values);
    }

    [Fact]
    public async Task ResetAsksFirstAndDoesNothingWhenDeclined()
    {
        _device.Setup(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);
        var sut = Sut();

        await sut.ResetLinkCommand.ExecuteAsync(null);

        _api.Verify(a => a.EnableAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetReplacesTheStoredLink()
    {
        _storage.Values["calendar_feed_url:ana@example.com"] = "https://g.test/old.ics";
        EnableReturns(Url);
        var sut = Sut();

        await sut.ResetLinkCommand.ExecuteAsync(null);

        Assert.Equal(Url, _storage.Values["calendar_feed_url:ana@example.com"]);
    }

    [Fact]
    public async Task TurnOffAsksFirstAndDoesNothingWhenDeclined()
    {
        _device.Setup(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);
        var sut = Sut();

        await sut.TurnOffCommand.ExecuteAsync(null);

        _api.Verify(a => a.DisableAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TurnOffForgetsTheLink()
    {
        StatusIs(true);
        _storage.Values["calendar_feed_url:ana@example.com"] = Url;
        _api.Setup(a => a.DisableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var sut = Sut();
        await sut.LoadCommand.ExecuteAsync(null);

        await sut.TurnOffCommand.ExecuteAsync(null);

        Assert.True(sut.IsOff);
        Assert.Empty(_storage.Values);
    }

    [Fact]
    public async Task AFailedTurnOffKeepsTheLink()
    {
        StatusIs(true);
        _storage.Values["calendar_feed_url:ana@example.com"] = Url;
        _api.Setup(a => a.DisableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var sut = Sut();
        await sut.LoadCommand.ExecuteAsync(null);

        await sut.TurnOffCommand.ExecuteAsync(null);

        Assert.True(sut.HasError);
        Assert.True(sut.IsOnWithLink);
        Assert.Equal(Url, _storage.Values["calendar_feed_url:ana@example.com"]);
    }

    [Theory]
    [InlineData(true, "Add to Apple Calendar", "Add to Google Calendar")]
    [InlineData(false, "Add to Google Calendar", "Add to Apple Calendar")]
    public void TheNativeCalendarLeads(bool apple, string primary, string secondary)
    {
        _device.SetupGet(d => d.PrefersAppleCalendar).Returns(apple);
        var sut = Sut();

        Assert.Equal(primary, sut.PrimarySubscribeLabel);
        Assert.Equal(secondary, sut.SecondarySubscribeLabel);
    }

    [Theory]
    [InlineData(true, "webcal://g.test/api/v1/calendar/feed/token.ics")]
    [InlineData(false, "https://calendar.google.com/calendar/r?cid=webcal%3A%2F%2Fg.test%2Fapi%2Fv1%2Fcalendar%2Ffeed%2Ftoken.ics")]
    public async Task ThePrimaryActionOpensThatCalendarsLink(bool apple, string expected)
    {
        _device.SetupGet(d => d.PrefersAppleCalendar).Returns(apple);
        StatusIs(true);
        _storage.Values["calendar_feed_url:ana@example.com"] = Url;
        var sut = Sut();
        await sut.LoadCommand.ExecuteAsync(null);

        await sut.SubscribePrimaryCommand.ExecuteAsync(null);

        _device.Verify(d => d.OpenAsync(expected));
    }

    [Fact]
    public async Task ACalendarThatWillNotOpenPointsAtCopy()
    {
        _device.Setup(d => d.OpenAsync(It.IsAny<string>())).ReturnsAsync(false);
        StatusIs(true);
        _storage.Values["calendar_feed_url:ana@example.com"] = Url;
        var sut = Sut();
        await sut.LoadCommand.ExecuteAsync(null);

        await sut.SubscribeSecondaryCommand.ExecuteAsync(null);

        Assert.True(sut.HasError);
    }

    [Fact]
    public async Task CopyCopiesTheHttpsLink()
    {
        StatusIs(true);
        _storage.Values["calendar_feed_url:ana@example.com"] = Url;
        var sut = Sut();
        await sut.LoadCommand.ExecuteAsync(null);

        await sut.CopyLinkCommand.ExecuteAsync(null);

        _device.Verify(d => d.CopyAsync(Url));
        Assert.True(sut.HasStatus);
    }

    private sealed class FakeStorage : ISecureStorageService
    {
        public Dictionary<string, string> Values { get; } = [];

        public Task<string?> GetAsync(string key) =>
            Task.FromResult(Values.TryGetValue(key, out var value) ? value : null);

        public Task SetAsync(string key, string value)
        {
            Values[key] = value;
            return Task.CompletedTask;
        }

        public void Remove(string key) => Values.Remove(key);
    }
}
