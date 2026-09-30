using AgendaBuddy.Library.Showcase;
using AgendaBuddy.Provider.Core.Showcase;
using AgendaBuddy.Provider.Domain.Showcase;
using static AgendaBuddy.Provider.Tests.Showcase.ShowcaseHandlerTestSupport;

namespace AgendaBuddy.Provider.Tests.Showcase;

public class CreatePublicCodeCommandHandlerTest
{
    private readonly Mock<IShowcaseService> _showcaseService = new();
    private readonly Mock<IEventStore> _eventStore = new();
    private readonly ProviderEntity _provider = NewProvider();

    private CreatePublicCodeCommandHandler Build(string? baseUrl) =>
        new(_showcaseService.Object, _eventStore.Object,
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [CreatePublicCodeCommandHandler.GoBaseUrlKey] = baseUrl
                })
                .Build());

    [Theory]
    [InlineData("https://go.agendame.app")]
    [InlineData("https://go.agendame.app/")]
    public async Task Handle_ReturnsTheCodeAndItsGoUrl(string baseUrl)
    {
        _showcaseService.Setup(s => s.FindProviderByEmailAsync(OwnerEmail)).ReturnsAsync(_provider);
        _showcaseService.Setup(s => s.GetOrCreatePublicCodeAsync(_provider)).ReturnsAsync("ABC234");

        var result = await Build(baseUrl).Handle(new CreatePublicCodeCommand { Email = OwnerEmail },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("ABC234", result.Value.Code);
        Assert.Equal("https://go.agendame.app/api/v1/go/ABC234", result.Value.Url);
        VerifyAudited(_eventStore, nameof(CreatePublicCodeCommand), "Success");
    }

    [Fact]
    public async Task Handle_AnUnknownProvider_IsNotFoundWithoutAllocatingACode()
    {
        _showcaseService.Setup(s => s.FindProviderByEmailAsync(OwnerEmail)).ReturnsAsync((ProviderEntity?)null);

        var result = await Build("https://go.agendame.app").Handle(new CreatePublicCodeCommand { Email = OwnerEmail },
            CancellationToken.None);

        Assert.Equal(ShowcaseErrorKind.NotFound, Error(result).Kind);
        _showcaseService.Verify(s => s.GetOrCreatePublicCodeAsync(It.IsAny<ProviderEntity>()), Times.Never);
        VerifyAudited(_eventStore, nameof(CreatePublicCodeCommand), "Failed");
    }
}
