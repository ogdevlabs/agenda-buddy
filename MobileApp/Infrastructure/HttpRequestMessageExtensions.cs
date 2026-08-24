namespace MobileApp.Infrastructure;

/// <summary>
/// An <see cref="HttpRequestMessage"/> can only be sent once — the framework disposes its content
/// stream after the first send. <see cref="JwtDelegatingHandler"/> needs a second attempt at the
/// same logical request after a transparent token refresh (AC9 of F-015-T09), so it clones the
/// request up front, before the first send consumes it.
/// </summary>
public static class HttpRequestMessageExtensions
{
    public static async Task<HttpRequestMessage> CloneAsync(this HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
        };

        if (request.Content is not null)
        {
            var buffer = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(buffer);

            foreach (var header in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        foreach (var option in request.Options)
            clone.Options.TryAdd(option.Key, option.Value);

        return clone;
    }
}
