using AgendaBuddy.Library.Media;
using AgendaBuddy.Library.Showcase;
using AgendaBuddy.Provider.Core.Showcase;
using AgendaBuddy.Provider.Domain.Showcase;
using static AgendaBuddy.Provider.Tests.Showcase.ShowcaseHandlerTestSupport;

namespace AgendaBuddy.Provider.Tests.Showcase;

public class UploadMediaCommandHandlerTest
{
    private readonly Mock<IShowcaseService> _showcaseService = new();
    private readonly Mock<IMediaService> _mediaService = new();
    private readonly Mock<IEventStore> _eventStore = new();
    private readonly List<Event> _audits = [];
    private readonly ProviderEntity _provider = NewProvider();
    private readonly byte[] _content = [0xFF, 0xD8, 0xFF, 0x42, 0x43, 0x44];

    public UploadMediaCommandHandlerTest()
    {
        _showcaseService.Setup(s => s.FindProviderByEmailAsync(OwnerEmail)).ReturnsAsync(_provider);
        _eventStore.Setup(e => e.SaveAsync(It.IsAny<Event>()))
                   .Callback<Event>(_audits.Add)
                   .Returns(Task.CompletedTask);
    }

    private UploadMediaCommandHandler Build() => new(_showcaseService.Object, _mediaService.Object, _eventStore.Object);

    private UploadMediaCommand Command() => new() { Email = OwnerEmail, Content = _content };

    private void UploadReturns(MediaUploadResult result) =>
        _mediaService.Setup(m => m.UploadAsync(_provider, _content, It.IsAny<CancellationToken>())).ReturnsAsync(result);

    [Theory]
    [InlineData(MediaUploadStatus.Created, false)]
    [InlineData(MediaUploadStatus.Deduplicated, true)]
    public async Task Handle_AStoredUpload_ReturnsTheHash(MediaUploadStatus status, bool deduplicated)
    {
        UploadReturns(new MediaUploadResult(status, Hash(), 800, 600));

        var result = await Build().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new MediaUploadView(Hash(), 800, 600, deduplicated), result.Value);
        VerifyAudited(_eventStore, nameof(UploadMediaCommand), "Success");
    }

    [Fact]
    public async Task Handle_StorageUnavailable_IsAStorageUnavailableError()
    {
        _mediaService.Setup(m => m.UploadAsync(_provider, _content, It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new MediaStorageUnavailableException("down"));

        var result = await Build().Handle(Command(), CancellationToken.None);

        var error = Error(result);
        Assert.Equal(ShowcaseErrorKind.StorageUnavailable, error.Kind);
        Assert.Equal("storage-unavailable", error.Code);
        VerifyAudited(_eventStore, nameof(UploadMediaCommand), "Failed");
    }

    [Fact]
    public async Task Handle_RateLimited_CarriesRetryAfter()
    {
        UploadReturns(new MediaUploadResult(MediaUploadStatus.RateLimited, RetryAfter: TimeSpan.FromMinutes(7)));

        var result = await Build().Handle(Command(), CancellationToken.None);

        var error = Error(result);
        Assert.Equal(ShowcaseErrorKind.RateLimited, error.Kind);
        Assert.Equal(TimeSpan.FromMinutes(7), error.RetryAfter);
        VerifyAudited(_eventStore, nameof(UploadMediaCommand), "Failed");
    }

    [Fact]
    public async Task Handle_ARejectedImage_IsInvalidWithTheRejectionCode()
    {
        UploadReturns(new MediaUploadResult(MediaUploadStatus.Rejected, Rejection: ImageRejections.TooLarge));

        var result = await Build().Handle(Command(), CancellationToken.None);

        var error = Error(result);
        Assert.Equal(ShowcaseErrorKind.Invalid, error.Kind);
        Assert.Equal("image", error.Field);
        Assert.Equal(ImageRejections.TooLarge, error.Code);
        Assert.Equal(ImageRejections.Describe(ImageRejections.TooLarge), error.Message);
        Assert.Contains(ImageRejections.TooLarge, Assert.Single(_audits).Data);
    }

    [Fact]
    public async Task Handle_AnUnknownProvider_IsNotFoundWithoutUploading()
    {
        _showcaseService.Setup(s => s.FindProviderByEmailAsync(OwnerEmail)).ReturnsAsync((ProviderEntity?)null);

        var result = await Build().Handle(Command(), CancellationToken.None);

        Assert.Equal(ShowcaseErrorKind.NotFound, Error(result).Kind);
        _mediaService.VerifyNoOtherCalls();
        VerifyAudited(_eventStore, nameof(UploadMediaCommand), "Failed");
    }

    [Fact]
    public async Task Handle_TheAuditRecordsTheSizeAndHashButNeverTheBytes()
    {
        UploadReturns(new MediaUploadResult(MediaUploadStatus.Created, Hash(), 1, 1));

        await Build().Handle(Command(), CancellationToken.None);

        var data = Assert.Single(_audits).Data;
        Assert.Contains("\"bytes\":6", data);
        Assert.Contains(Hash(), data);
        Assert.DoesNotContain(Convert.ToBase64String(_content), data);
        Assert.DoesNotContain("Content", data);
    }
}
