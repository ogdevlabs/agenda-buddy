using System.Net.Http;
using AgendaBuddy.Library.Showcase;
using AgendaBuddy.Provider.Core.Showcase;
using AgendaBuddy.Provider.Domain.Showcase;
using Microsoft.Extensions.Logging.Abstractions;
using static AgendaBuddy.Provider.Tests.Showcase.ShowcaseHandlerTestSupport;

namespace AgendaBuddy.Provider.Tests.Showcase;

public class ReportShowcaseCommandHandlerTest
{
    private const string OperatorEmail = "ops@agendame.app";

    private readonly Mock<IShowcaseService> _showcaseService = new();
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly Mock<IEventStore> _eventStore = new();
    private readonly ProviderEntity _provider = NewProvider();

    public ReportShowcaseCommandHandlerTest()
    {
        _showcaseService.Setup(s => s.FindProviderByRefAsync(_provider.Id.ToString())).ReturnsAsync(_provider);
        _showcaseService.Setup(s => s.IsVisibleToAsync(_provider, It.IsAny<ProviderShowcaseEntity?>(), It.IsAny<string>()))
                        .ReturnsAsync(true);
        _showcaseService.Setup(s => s.ReportAsync(_provider.Id, It.IsAny<string>(), It.IsAny<string>(),
                             It.IsAny<string?>(), It.IsAny<string?>()))
                        .ReturnsAsync(true);
    }

    private ReportShowcaseCommandHandler Build(string? operatorEmail = OperatorEmail) =>
        new(_showcaseService.Object, _emailSender.Object, _eventStore.Object,
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [ReportShowcaseCommandHandler.ReportEmailKey] = operatorEmail
                })
                .Build(),
            NullLogger<ReportShowcaseCommandHandler>.Instance);

    private ReportShowcaseCommand Command(string reason = "spam", string? detail = null,
        string reporter = ViewerEmail, string? portfolioHash = null) =>
        new()
        {
            ReporterEmail = reporter,
            ProviderRef = _provider.Id.ToString(),
            Reason = reason,
            Detail = detail,
            PortfolioHash = portfolioHash
        };

    [Fact]
    public async Task Handle_AValidReport_IsStoredAndTheOperatorEmailed()
    {
        var result = await Build().Handle(Command(" SPAM ", portfolioHash: Hash()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _showcaseService.Verify(s => s.ReportAsync(_provider.Id, ViewerEmail, "spam", null, Hash()), Times.Once);
        _emailSender.Verify(e => e.SendAsync(OperatorEmail, It.IsAny<string>(),
            It.Is<string>(b => b.Contains(_provider.Id.ToString()) && !b.Contains(ViewerEmail)),
            It.IsAny<CancellationToken>()), Times.Once);
        VerifyAudited(_eventStore, nameof(ReportShowcaseCommand), "Success");
    }

    [Fact]
    public async Task Handle_ReportingYourself_IsNotFound()
    {
        var result = await Build().Handle(Command(reporter: OwnerEmail.ToUpperInvariant()), CancellationToken.None);

        Assert.Equal(ShowcaseErrorKind.NotFound, Error(result).Kind);
        _showcaseService.Verify(s => s.ReportAsync(It.IsAny<ObjectId>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
        VerifyAudited(_eventStore, nameof(ReportShowcaseCommand), "Failed");
    }

    [Fact]
    public async Task Handle_AProviderNotVisibleToTheReporter_IsNotFound()
    {
        _showcaseService.Setup(s => s.IsVisibleToAsync(_provider, It.IsAny<ProviderShowcaseEntity?>(), ViewerEmail))
                        .ReturnsAsync(false);

        var result = await Build().Handle(Command(), CancellationToken.None);

        Assert.Equal(ShowcaseErrorKind.NotFound, Error(result).Kind);
        VerifyAudited(_eventStore, nameof(ReportShowcaseCommand), "Failed");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Handle_OtherWithoutDetail_IsInvalid(string? detail)
    {
        var result = await Build().Handle(Command("other", detail), CancellationToken.None);

        var error = Error(result);
        Assert.Equal(ShowcaseErrorKind.Invalid, error.Kind);
        Assert.Equal("detail", error.Field);
        Assert.Equal("required", error.Code);
        VerifyAudited(_eventStore, nameof(ReportShowcaseCommand), "Failed");
    }

    [Fact]
    public async Task Handle_AnUnknownReason_IsInvalid()
    {
        var result = await Build().Handle(Command("boring"), CancellationToken.None);

        Assert.Equal("invalid-reason", Error(result).Code);
        VerifyAudited(_eventStore, nameof(ReportShowcaseCommand), "Failed");
    }

    [Fact]
    public async Task Handle_DetailTooLong_IsInvalid()
    {
        var result = await Build().Handle(Command("other", new string('a', ShowcaseRules.MaxReportDetail + 1)),
            CancellationToken.None);

        Assert.Equal("too-long", Error(result).Code);
    }

    [Fact]
    public async Task Handle_TheMailProviderThrowing_StillSucceeds()
    {
        _emailSender.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new HttpRequestException("mail down"));

        var result = await Build().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        VerifyAudited(_eventStore, nameof(ReportShowcaseCommand), "Success");
    }

    [Fact]
    public async Task Handle_NoOperatorAddressConfigured_StillSucceedsWithoutEmailing()
    {
        var result = await Build(operatorEmail: null).Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _emailSender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ARepeatReportWithinTheDebounce_SucceedsWithoutEmailing()
    {
        _showcaseService.Setup(s => s.ReportAsync(_provider.Id, It.IsAny<string>(), It.IsAny<string>(),
                             It.IsAny<string?>(), It.IsAny<string?>()))
                        .ReturnsAsync(false);

        var result = await Build().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _emailSender.VerifyNoOtherCalls();
        VerifyAudited(_eventStore, nameof(ReportShowcaseCommand), "Success");
    }

    [Fact]
    public async Task Handle_AMalformedPortfolioHash_IsReportedAsTheWholeShowcase()
    {
        await Build().Handle(Command(portfolioHash: "nope"), CancellationToken.None);

        _showcaseService.Verify(s => s.ReportAsync(_provider.Id, ViewerEmail, "spam", null, null), Times.Once);
    }
}
