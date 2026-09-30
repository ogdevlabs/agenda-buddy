using System.Collections.Concurrent;

namespace AgendaBuddy.Library.Media;

/// <summary>Process-local blob store for tests and standalone runs; nothing survives a restart.</summary>
public sealed class InMemoryBlobStore : IBlobStore
{
    private readonly ConcurrentDictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);

    public Task PutAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        _blobs[key] = content.ToArray();
        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream?>(_blobs.TryGetValue(key, out var bytes) ? new MemoryStream(bytes, writable: false) : null);

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_blobs.ContainsKey(key));

    public Task DeletePrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        foreach (var key in _blobs.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)))
            _blobs.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(
            _blobs.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).Order().ToList());

    public Task<IReadOnlyList<string>> ListTopLevelPrefixesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(
            _blobs.Keys.Select(key => key.Split('/')[0]).Distinct().Order().ToList());
}
