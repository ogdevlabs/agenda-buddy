namespace AgendaBuddy.Library.Media;

/// <summary>
/// Registered when no media storage is configured. Uploads and fetches answer 503 instead of silently keeping
/// images in memory, where a restart would lose them behind a successful response.
/// </summary>
public sealed class UnavailableBlobStore : IBlobStore
{
    private static MediaStorageUnavailableException Fail() =>
        new("Media storage is not configured: set ConnectionStrings:media, or Showcase:Media:InMemory=true for a local run.");

    public Task PutAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken = default) =>
        throw Fail();

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default) => throw Fail();

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => throw Fail();

    public Task DeletePrefixAsync(string prefix, CancellationToken cancellationToken = default) => throw Fail();

    public Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken cancellationToken = default) =>
        throw Fail();

    public Task<IReadOnlyList<string>> ListTopLevelPrefixesAsync(CancellationToken cancellationToken = default) =>
        throw Fail();
}
