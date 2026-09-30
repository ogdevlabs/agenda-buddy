using AgendaBuddy.Library.Showcase;
using AgendaBuddy.Provider.Core.Showcase;
using AgendaBuddy.Provider.Domain.Showcase;
using static AgendaBuddy.Provider.Tests.Showcase.ShowcaseHandlerTestSupport;

namespace AgendaBuddy.Provider.Tests.Showcase;

public class SetShowcaseTextCommandHandlerTest
{
    private readonly Mock<IShowcaseService> _showcaseService = new();
    private readonly Mock<IEventStore> _eventStore = new();
    private readonly ProviderEntity _provider = NewProvider();

    private SetShowcaseTextCommandHandler Build() => new(_showcaseService.Object, _eventStore.Object);

    [Fact]
    public async Task Handle_ValidText_IsTrimmedWrittenAndAudited()
    {
        _showcaseService.Setup(s => s.FindProviderByEmailAsync(OwnerEmail)).ReturnsAsync(_provider);
        _showcaseService.Setup(s => s.SetTextAsync(_provider, It.IsAny<string?>(), It.IsAny<string?>()))
                        .ReturnsAsync(new ProviderShowcaseEntity { Tagline = "Strength coach" });

        var result = await Build().Handle(
            new SetShowcaseTextCommand { Email = OwnerEmail, Tagline = "  Strength coach  ", About = "   " },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Strength coach", result.Value.Tagline);
        _showcaseService.Verify(s => s.SetTextAsync(_provider, "Strength coach", null), Times.Once);
        VerifyAudited(_eventStore, nameof(SetShowcaseTextCommand), "Success");
    }

    [Fact]
    public async Task Handle_ATaglineTooLong_IsRejectedWithoutWritingButAudited()
    {
        var result = await Build().Handle(
            new SetShowcaseTextCommand { Email = OwnerEmail, Tagline = new string('a', ShowcaseRules.MaxTagline + 1) },
            CancellationToken.None);

        var error = Error(result);
        Assert.Equal(ShowcaseErrorKind.Invalid, error.Kind);
        Assert.Equal("tagline", error.Field);
        Assert.Equal("too-long", error.Code);
        _showcaseService.Verify(s => s.SetTextAsync(It.IsAny<ProviderEntity>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Never);
        VerifyAudited(_eventStore, nameof(SetShowcaseTextCommand), "Failed");
    }

    [Fact]
    public async Task Handle_AboutTooLong_IsRejected()
    {
        var result = await Build().Handle(
            new SetShowcaseTextCommand { Email = OwnerEmail, About = new string('a', ShowcaseRules.MaxAbout + 1) },
            CancellationToken.None);

        Assert.Equal("about", Error(result).Field);
        VerifyAudited(_eventStore, nameof(SetShowcaseTextCommand), "Failed");
    }

    [Fact]
    public async Task Handle_AnUnknownProvider_IsNotFoundAndAudited()
    {
        _showcaseService.Setup(s => s.FindProviderByEmailAsync(OwnerEmail)).ReturnsAsync((ProviderEntity?)null);

        var result = await Build().Handle(new SetShowcaseTextCommand { Email = OwnerEmail, Tagline = "Hi" },
            CancellationToken.None);

        Assert.Equal(ShowcaseErrorKind.NotFound, Error(result).Kind);
        VerifyAudited(_eventStore, nameof(SetShowcaseTextCommand), "Failed");
    }
}
