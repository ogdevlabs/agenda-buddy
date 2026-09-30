using System.Linq;
using AgendaBuddy.Library.Showcase;
using AgendaBuddy.Provider.Core.Showcase;
using AgendaBuddy.Provider.Domain.Showcase;
using static AgendaBuddy.Provider.Tests.Showcase.ShowcaseHandlerTestSupport;

namespace AgendaBuddy.Provider.Tests.Showcase;

public class ReorderPortfolioCommandHandlerTest
{
    private readonly Mock<IShowcaseService> _showcaseService = new();
    private readonly Mock<IEventStore> _eventStore = new();
    private readonly ProviderEntity _provider = NewProvider();

    public ReorderPortfolioCommandHandlerTest() =>
        _showcaseService.Setup(s => s.FindProviderByEmailAsync(OwnerEmail)).ReturnsAsync(_provider);

    private ReorderPortfolioCommandHandler Build() => new(_showcaseService.Object, _eventStore.Object);

    [Fact]
    public async Task Handle_APermutation_ReturnsTheReorderedShowcase()
    {
        var hashes = new List<string> { Hash('b'), Hash('a') };
        var showcase = new ProviderShowcaseEntity
        {
            Portfolio = [new PortfolioItem { Hash = Hash('b') }, new PortfolioItem { Hash = Hash('a') }]
        };
        _showcaseService.Setup(s => s.ReorderPortfolioAsync(_provider, hashes))
                        .ReturnsAsync(new ShowcaseWriteResult(ShowcaseWriteStatus.Ok, showcase));

        var result = await Build().Handle(new ReorderPortfolioCommand { Email = OwnerEmail, Hashes = hashes },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(hashes, result.Value.Portfolio.Select(i => i.Hash));
        VerifyAudited(_eventStore, nameof(ReorderPortfolioCommand), "Success");
    }

    [Theory]
    [InlineData(ShowcaseWriteStatus.PortfolioChanged)]
    [InlineData(ShowcaseWriteStatus.NotFound)]
    public async Task Handle_AListThatIsNotThePortfolio_IsAPortfolioChangedConflict(ShowcaseWriteStatus status)
    {
        _showcaseService.Setup(s => s.ReorderPortfolioAsync(_provider, It.IsAny<IReadOnlyList<string>>()))
                        .ReturnsAsync(new ShowcaseWriteResult(status));

        var result = await Build().Handle(new ReorderPortfolioCommand { Email = OwnerEmail, Hashes = [Hash()] },
            CancellationToken.None);

        var error = Error(result);
        Assert.Equal(ShowcaseErrorKind.Conflict, error.Kind);
        Assert.Equal("portfolio-changed", error.Code);
        VerifyAudited(_eventStore, nameof(ReorderPortfolioCommand), "Failed");
    }

    [Fact]
    public async Task Handle_AnUnknownProvider_IsNotFound()
    {
        _showcaseService.Setup(s => s.FindProviderByEmailAsync(OwnerEmail)).ReturnsAsync((ProviderEntity?)null);

        var result = await Build().Handle(new ReorderPortfolioCommand { Email = OwnerEmail, Hashes = [] },
            CancellationToken.None);

        Assert.Equal(ShowcaseErrorKind.NotFound, Error(result).Kind);
        VerifyAudited(_eventStore, nameof(ReorderPortfolioCommand), "Failed");
    }
}
