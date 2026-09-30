namespace AgendaBuddy.MobileApp.Services;

/// <summary>
/// Picks photos from the device and hands them back ready to upload: resized so the long edge is at most
/// <see cref="MaxLongEdge"/> and encoded as JPEG.
/// </summary>
/// <remarks>
/// Resizing on the device is what keeps an upload of a 48-megapixel camera original to a few hundred kilobytes;
/// the server re-encodes whatever arrives, so this is about the upload's size and time, not its safety.
/// The system photo picker is used, which needs no photo-library permission.
/// </remarks>
public interface IImagePicker
{
    const int MaxLongEdge = 1600;

    /// <param name="limit">The most photos to accept. At least 1.</param>
    /// <returns>One JPEG per chosen photo; empty when the picker was cancelled.</returns>
    Task<IReadOnlyList<byte[]>> PickAsync(int limit, CancellationToken ct = default);
}

/// <summary>Stands in where there is no photo picker, such as the <c>net10.0</c> slice.</summary>
public sealed class UnavailableImagePicker : IImagePicker
{
    public Task<IReadOnlyList<byte[]>> PickAsync(int limit, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<byte[]>>(Array.Empty<byte[]>());
}
