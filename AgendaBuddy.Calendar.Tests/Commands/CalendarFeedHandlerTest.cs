using AgendaBuddy.Calendar.Core.Commands;
using AgendaBuddy.Calendar.Domain.Commands;

namespace AgendaBuddy.Calendar.Tests.Commands;

public class CalendarFeedHandlerTest
{
    private const string Owner = "coach@example.com";
    private const string Token = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Fact]
    public async Task EnableReturnsTheTokenAndAuditsWithoutIt()
    {
        var feeds = new Mock<ICalendarFeedService>();
        feeds.Setup(f => f.EnableAsync(Owner, "es")).ReturnsAsync(new EnabledCalendarFeed(Token, DateTime.UtcNow));
        var events = new Mock<IEventStore>();
        Event? audited = null;
        events.Setup(e => e.SaveAsync(It.IsAny<Event>())).Callback<Event>(e => audited = e).Returns(Task.CompletedTask);

        var result = await new EnableCalendarFeedCommandHandler(feeds.Object, events.Object)
            .Handle(new EnableCalendarFeedCommand { OwnerEmail = Owner, Language = "es" }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Token, result.Value.Token);
        Assert.NotNull(audited);
        Assert.Equal(nameof(EnableCalendarFeedCommand), audited!.Type);
        Assert.DoesNotContain(Token, audited.Data);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DisableSucceedsWhetherOrNotAFeedExisted(bool existed)
    {
        var feeds = new Mock<ICalendarFeedService>();
        feeds.Setup(f => f.DisableAsync(Owner)).ReturnsAsync(existed);
        var events = new Mock<IEventStore>();

        var result = await new DisableCalendarFeedCommandHandler(feeds.Object, events.Object)
            .Handle(new DisableCalendarFeedCommand { OwnerEmail = Owner }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(existed, result.Value);
        events.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Type == nameof(DisableCalendarFeedCommand))));
    }

    [Fact]
    public async Task StatusReportsTheFeedWithoutItsToken()
    {
        var created = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        var feeds = new Mock<ICalendarFeedService>();
        feeds.Setup(f => f.GetAsync(Owner))
            .ReturnsAsync(new CalendarFeedEntity(Owner, "hash", "en") { CreatedAt = created });
        var events = new Mock<IEventStore>();

        var result = await new GetCalendarFeedStatusQueryHandler(feeds.Object, events.Object)
            .Handle(new GetCalendarFeedStatusQuery { OwnerEmail = Owner }, CancellationToken.None);

        Assert.Equal(new CalendarFeedStatus(true, created), result.Value);
        events.Verify(e => e.SaveAsync(It.IsAny<Event>()));
    }

    [Fact]
    public async Task StatusIsDisabledWhenThereIsNoFeed()
    {
        var feeds = new Mock<ICalendarFeedService>();
        feeds.Setup(f => f.GetAsync(Owner)).ReturnsAsync((CalendarFeedEntity?)null);

        var result = await new GetCalendarFeedStatusQueryHandler(feeds.Object, new Mock<IEventStore>().Object)
            .Handle(new GetCalendarFeedStatusQuery { OwnerEmail = Owner }, CancellationToken.None);

        Assert.Equal(new CalendarFeedStatus(false, null), result.Value);
    }
}
