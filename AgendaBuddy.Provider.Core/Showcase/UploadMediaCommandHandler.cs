namespace AgendaBuddy.Provider.Core.Showcase;

/// <summary>
/// Stores an upload. The audit records the size and the resulting hash, never the bytes: an image in the
/// <c>events</c> collection would be personal data nothing erases.
/// </summary>
public class UploadMediaCommandHandler(
    IShowcaseService showcaseService,
    IMediaService mediaService,
    IEventStore eventStore)
    : IRequestHandler<UploadMediaCommand, Result<MediaUploadView>>
{
    public async Task<Result<MediaUploadView>> Handle(UploadMediaCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var provider = await showcaseService.FindProviderByEmailAsync(request.Email);
        if (provider is null)
            return await FailAsync(request, null, ShowcaseError.NotFound());

        MediaUploadResult result;
        try
        {
            result = await mediaService.UploadAsync(provider, request.Content, cancellationToken);
        }
        catch (MediaStorageUnavailableException)
        {
            return await FailAsync(request, null, ShowcaseError.StorageUnavailable());
        }

        switch (result.Status)
        {
            case MediaUploadStatus.Created or MediaUploadStatus.Deduplicated when result.Hash is not null:
                await SaveAsync("Success", request, result.Hash);
                return Result.Ok(new MediaUploadView(result.Hash, result.Width, result.Height,
                    result.Status == MediaUploadStatus.Deduplicated));
            case MediaUploadStatus.RateLimited:
                return await FailAsync(request, null,
                    ShowcaseError.RateLimited(result.RetryAfter ?? TimeSpan.FromMinutes(1)));
            default:
                var code = result.Rejection ?? ImageRejections.Undecodable;
                return await FailAsync(request, code,
                    ShowcaseError.Invalid("image", code, ImageRejections.Describe(code)));
        }
    }

    private async Task<Result<MediaUploadView>> FailAsync(UploadMediaCommand request, string? rejection, ShowcaseError error)
    {
        await SaveAsync("Failed", request, rejection ?? error.Code);
        return Result.Fail(error);
    }

    private Task SaveAsync(string status, UploadMediaCommand request, string outcome) =>
        eventStore.SaveAsync(ShowcaseProjection.Audit(nameof(UploadMediaCommand), status,
            new { request.Email, bytes = request.Content.Length, outcome }));
}
