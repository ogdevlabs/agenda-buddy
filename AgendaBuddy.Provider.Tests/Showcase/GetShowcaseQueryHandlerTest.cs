using System.Linq;
using AgendaBuddy.Library.Showcase;
using AgendaBuddy.Provider.Core.Showcase;
using AgendaBuddy.Provider.Domain.Showcase;
using static AgendaBuddy.Provider.Tests.Showcase.ShowcaseHandlerTestSupport;

namespace AgendaBuddy.Provider.Tests.Showcase;

public class GetShowcaseQueryHandlerTest
{
    private readonly Mock<IShowcaseService> _showcaseService = new();
    private readonly Mock<IEventStore> _eventStore = new();
    private readonly ProviderEntity _provider = NewProvider();
    private readonly ProviderShowcaseEntity _showcase;

    public GetShowcaseQueryHandlerTest()
    {
        _showcase = new ProviderShowcaseEntity
        {
            ProviderId = _provider.Id,
            Tagline = "Strength coach",
            Portfolio =
            [
                new PortfolioItem { Hash = Hash('a'), AddedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc) },
                new PortfolioItem { Hash = Hash('b'), AddedAt = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc) }
            ]
        };
        _showcaseService.Setup(s => s.FindProviderByRefAsync(_provider.Id.ToString())).ReturnsAsync(_provider);
        _showcaseService.Setup(s => s.FindShowcaseAsync(_provider.Id)).ReturnsAsync(_showcase);
        _showcaseService.Setup(s => s.IsVisibleToAsync(_provider, _showcase, It.IsAny<string>())).ReturnsAsync(true);
        Relationship(isSelf: false);
    }

    private GetShowcaseQueryHandler Build() => new(_showcaseService.Object, _eventStore.Object);

    private void Relationship(bool isSelf) =>
        _showcaseService.Setup(s => s.GetRelationshipAsync(_provider, It.IsAny<string>()))
                        .ReturnsAsync(new ShowcaseRelationship(isSelf, false, null, false));

    private GetShowcaseQuery ByRef(string caller = ViewerEmail, string? source = null) =>
        new() { CallerEmail = caller, ProviderRef = _provider.Id.ToString(), Source = source };

    [Fact]
    public async Task Handle_AVisibleShowcase_IsReturnedAndTheVisitRecorded()
    {
        var result = await Build().Handle(ByRef(source: "message"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(_provider.Id.ToString(), result.Value.ProviderRef);
        Assert.Equal("Strength coach", result.Value.Tagline);
        _showcaseService.Verify(s => s.RecordVisitAsync(_provider.Id, ViewerEmail, ShowcaseSources.Message), Times.Once);
        _eventStore.Verify(e => e.SaveAsync(It.Is<Event>(
            ev => ev.Type == nameof(GetShowcaseQuery) && ev.Status == "Success")), Times.Once);
    }

    [Fact]
    public async Task Handle_ItemsAddedSinceThePreviousVisit_AreMarkedNew()
    {
        _showcaseService.Setup(s => s.RecordVisitAsync(_provider.Id, ViewerEmail, It.IsAny<string>()))
                        .ReturnsAsync(new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc));

        var result = await Build().Handle(ByRef(), CancellationToken.None);

        Assert.Equal([false, true], result.Value.Portfolio.Select(i => i.IsNew));
    }

    [Fact]
    public async Task Handle_AFirstVisit_MarksNothingNew()
    {
        var result = await Build().Handle(ByRef(), CancellationToken.None);

        Assert.All(result.Value.Portfolio, i => Assert.False(i.IsNew));
    }

    [Fact]
    public async Task Handle_TheOwnerPreviewing_RecordsNoVisit()
    {
        Relationship(isSelf: true);

        var result = await Build().Handle(ByRef(OwnerEmail), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Relationship.IsSelf);
        _showcaseService.Verify(s => s.RecordVisitAsync(It.IsAny<ObjectId>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_AShowcaseNotVisibleToTheCaller_IsNotFoundAndAudited()
    {
        _showcaseService.Setup(s => s.IsVisibleToAsync(_provider, _showcase, ViewerEmail)).ReturnsAsync(false);

        var result = await Build().Handle(ByRef(), CancellationToken.None);

        Assert.Equal(ShowcaseErrorKind.NotFound, Error(result).Kind);
        _showcaseService.Verify(s => s.RecordVisitAsync(It.IsAny<ObjectId>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        _eventStore.Verify(e => e.SaveAsync(It.Is<Event>(
            ev => ev.Type == nameof(GetShowcaseQuery) && ev.Status == "Failed")), Times.Once);
    }

    [Fact]
    public async Task Handle_AnUnknownProvider_IsNotFound()
    {
        var result = await Build().Handle(
            new GetShowcaseQuery { CallerEmail = ViewerEmail, ProviderRef = ObjectId.GenerateNewId().ToString() },
            CancellationToken.None);

        Assert.Equal(ShowcaseErrorKind.NotFound, Error(result).Kind);
    }

    [Fact]
    public async Task Handle_ByCode_RecordsACodeVisit()
    {
        _showcaseService.Setup(s => s.FindShowcaseByCodeAsync("ABC234")).ReturnsAsync(_showcase);

        var result = await Build().Handle(new GetShowcaseQuery { CallerEmail = ViewerEmail, Code = "ABC234" },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _showcaseService.Verify(s => s.RecordVisitAsync(_provider.Id, ViewerEmail, ShowcaseSources.Code), Times.Once);
    }

    [Fact]
    public async Task Handle_AnUnknownCode_IsNotFound()
    {
        var result = await Build().Handle(new GetShowcaseQuery { CallerEmail = ViewerEmail, Code = "ZZZZZZ" },
            CancellationToken.None);

        Assert.Equal(ShowcaseErrorKind.NotFound, Error(result).Kind);
    }

    [Fact]
    public async Task Handle_AProviderWithNoShowcaseDocument_AnswersWithTheEmptyState()
    {
        _showcaseService.Setup(s => s.FindShowcaseAsync(_provider.Id)).ReturnsAsync((ProviderShowcaseEntity?)null);
        _showcaseService.Setup(s => s.IsVisibleToAsync(_provider, null, ViewerEmail)).ReturnsAsync(true);

        var result = await Build().Handle(ByRef(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Portfolio);
        Assert.Null(result.Value.Tagline);
    }
}
