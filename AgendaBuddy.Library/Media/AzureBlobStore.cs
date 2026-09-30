using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace AgendaBuddy.Library.Media;

/// <summary>
/// A private container: no public access level, no SAS. Media is only ever reachable through the authenticated
/// proxy route (ADR-069).
/// </summary>
public sealed class AzureBlobStore(BlobContainerClient container) : IBlobStore
{
    private int _ensured;

    /// <summary>
    /// Accepts both shapes Aspire injects: a full connection string (the Azurite emulator) or a bare blob endpoint
    /// URI (a cloud account, reached with the app's managed identity).
    /// </summary>
    public static AzureBlobStore FromConnection(string connection, string containerName)
    {
        var service = Uri.TryCreate(connection, UriKind.Absolute, out var endpoint) && !connection.Contains(';')
            ? new BlobServiceClient(endpoint, new DefaultAzureCredential())
            : new BlobServiceClient(connection);
        return new AzureBlobStore(service.GetBlobContainerClient(containerName));
    }

    public async Task EnsureContainerAsync(CancellationToken cancellationToken = default)
    {
        await Guard(() => container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken));
        Interlocked.Exchange(ref _ensured, 1);
    }

    public async Task PutAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _ensured) == 0)
            await EnsureContainerAsync(cancellationToken);

        await Guard(() => container.GetBlobClient(key).UploadAsync(
            new BinaryData(content),
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
            cancellationToken));
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await container.GetBlobClient(key).DownloadStreamingAsync(cancellationToken: cancellationToken);
            return response.Value.Content;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            throw new MediaStorageUnavailableException("Media storage is unreachable.", ex);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return (await container.GetBlobClient(key).ExistsAsync(cancellationToken)).Value;
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            throw new MediaStorageUnavailableException("Media storage is unreachable.", ex);
        }
    }

    public async Task DeletePrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        foreach (var key in await ListAsync(prefix, cancellationToken))
        {
            await Guard(() => container.GetBlobClient(key)
                .DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken));
        }
    }

    public async Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var keys = new List<string>();
        try
        {
            await foreach (var blob in container.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
                keys.Add(blob.Name);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return [];
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            throw new MediaStorageUnavailableException("Media storage is unreachable.", ex);
        }

        return keys;
    }

    public async Task<IReadOnlyList<string>> ListTopLevelPrefixesAsync(CancellationToken cancellationToken = default)
    {
        var prefixes = new List<string>();
        try
        {
            await foreach (var item in container.GetBlobsByHierarchyAsync(delimiter: "/", cancellationToken: cancellationToken))
            {
                if (item.IsPrefix)
                    prefixes.Add(item.Prefix.TrimEnd('/'));
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return [];
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            throw new MediaStorageUnavailableException("Media storage is unreachable.", ex);
        }

        return prefixes;
    }

    private static async Task Guard(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            throw new MediaStorageUnavailableException("Media storage is unreachable.", ex);
        }
    }

    private static bool IsUnavailable(Exception ex) =>
        ex is RequestFailedException or AuthenticationFailedException or HttpRequestException or TaskCanceledException
            or AggregateException;
}
