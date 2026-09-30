using AgendaBuddy.Library.Showcase;
using AgendaBuddy.Provider.Core.Showcase;
using AgendaBuddy.Provider.Domain.Showcase;
using static AgendaBuddy.Provider.Tests.Showcase.ShowcaseHandlerTestSupport;

namespace AgendaBuddy.Provider.Tests.Showcase;

public class AddPortfolioItemCommandHandlerTest
{
    private readonly Mock<IShowcaseService> _showcaseService = new();
    private readonly Mock<IEventStore> _eventStore = new();
    private readonly ProviderEntity _provider = NewProvider();

    public AddPortfolioItemCommandHandlerTest() =>
        _showcaseService.Setup(s => s.FindProviderByEmailAsync(OwnerEmail)).ReturnsAsync(_provider);

    private AddPortfolioItemCommandHandler Build() => new(_showcaseService.Object, _eventStore.Object);

    private void AddReturns(ShowcaseWriteStatus status, PortfolioItem? item = null) =>
        _showcaseService.Setup(s => s.AddPortfolioItemAsync(_provider, It.IsAny<string>(), It.IsAny<string?>(),
                             It.IsAny<string?>()))
                        .ReturnsAsync(new ShowcaseWriteResult(status, Item: item));

    private static AddPortfolioItemCommand Command(string? hash = null, string? caption = null, string? serviceId = null) =>
        new() { Email = OwnerEmail, Hash = hash ?? Hash(), Caption = caption, ServiceId = serviceId };

    [Theory]
    [InlineData(ShowcaseWriteStatus.Ok, true)]
    [InlineData(ShowcaseWriteStatus.AlreadyPresent, false)]
    public async Task Handle_AnAcceptedAdd_ReturnsTheItemAndAuditsSuccess(ShowcaseWriteStatus status, bool created)
    {
        AddReturns(status, new PortfolioItem { Hash = Hash(), Caption = "Before", Width = 10, Height = 20 });

        var result = await Build().Handle(Command(caption: " Before "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(created, result.Value.Created);
        Assert.Equal(Hash(), result.Value.Item.Hash);
        _showcaseService.Verify(s => s.AddPortfolioItemAsync(_provider, Hash(), "Before", null), Times.Once);
        VerifyAudited(_eventStore, nameof(AddPortfolioItemCommand), "Success");
    }

    [Fact]
    public async Task Handle_AFullPortfolio_IsAPortfolioFullConflict()
    {
        AddReturns(ShowcaseWriteStatus.PortfolioFull);

        var result = await Build().Handle(Command(), CancellationToken.None);

        var error = Error(result);
        Assert.Equal(ShowcaseErrorKind.Conflict, error.Kind);
        Assert.Equal("portfolio-full", error.Code);
        VerifyAudited(_eventStore, nameof(AddPortfolioItemCommand), "Failed");
    }

    [Fact]
    public async Task Handle_MediaTheProviderDoesNotOwn_IsUnknownMedia()
    {
        AddReturns(ShowcaseWriteStatus.UnknownMedia);

        var result = await Build().Handle(Command(), CancellationToken.None);

        Assert.Equal("unknown-media", Error(result).Code);
        VerifyAudited(_eventStore, nameof(AddPortfolioItemCommand), "Failed");
    }

    [Fact]
    public async Task Handle_AMalformedHash_IsUnknownMediaWithoutALookup()
    {
        var result = await Build().Handle(Command(hash: "../etc/passwd"), CancellationToken.None);

        Assert.Equal("unknown-media", Error(result).Code);
        _showcaseService.Verify(s => s.FindProviderByEmailAsync(It.IsAny<string>()), Times.Never);
        VerifyAudited(_eventStore, nameof(AddPortfolioItemCommand), "Failed");
    }

    [Fact]
    public async Task Handle_ACaptionTooLong_IsInvalid()
    {
        var result = await Build().Handle(Command(caption: new string('a', ShowcaseRules.MaxCaption + 1)),
            CancellationToken.None);

        Assert.Equal("caption", Error(result).Field);
        VerifyAudited(_eventStore, nameof(AddPortfolioItemCommand), "Failed");
    }

    [Fact]
    public async Task Handle_AMalformedServiceId_IsInvalidService()
    {
        var result = await Build().Handle(Command(serviceId: "not-an-id"), CancellationToken.None);

        Assert.Equal("invalid-service", Error(result).Code);
        VerifyAudited(_eventStore, nameof(AddPortfolioItemCommand), "Failed");
    }

    [Fact]
    public async Task Handle_AServiceThatIsNotTheProviders_IsInvalidService()
    {
        AddReturns(ShowcaseWriteStatus.InvalidService);

        var result = await Build().Handle(Command(serviceId: ObjectId.GenerateNewId().ToString()),
            CancellationToken.None);

        Assert.Equal("invalid-service", Error(result).Code);
        VerifyAudited(_eventStore, nameof(AddPortfolioItemCommand), "Failed");
    }
}
