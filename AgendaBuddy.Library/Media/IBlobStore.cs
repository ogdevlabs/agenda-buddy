namespace AgendaBuddy.Library.Media;

/// <summary>Bytes for showcase media, keyed <c>{providerId}/{hash}/{variant}</c>.</summary>
/// <remarks>Every failure to reach the store surfaces as <see cref="MediaStorageUnavailableException"/>.</remarks>
public interface IBlobStore
{
    Task PutAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken = default);

    /// <summary><c>null</c> when no blob has that key.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    Task DeletePrefixAsync(string prefix, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>The distinct first path segments — one per provider that holds any blob.</summary>
    Task<IReadOnlyList<string>> ListTopLevelPrefixesAsync(CancellationToken cancellationToken = default);
}

public sealed class MediaStorageUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);

public static class MediaKeys
{
    public const string Full = "full";
    public const string Thumb = "thumb";

    public static string For(ObjectId providerId, string hash, string variant) => $"{providerId}/{hash}/{variant}";

    public static string Prefix(ObjectId providerId) => $"{providerId}/";

    public static bool IsVariant(string? variant) => variant is Full or Thumb;
}
