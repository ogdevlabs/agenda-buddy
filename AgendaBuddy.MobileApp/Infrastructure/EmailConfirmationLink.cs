namespace AgendaBuddy.MobileApp.Infrastructure;

public sealed record EmailConfirmationLink(string? Token)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(Token);

    public static EmailConfirmationLink Parse(Uri uri)
    {
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.Host, "agendame.app", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.AbsolutePath.Trim('/'), "confirm-email", StringComparison.OrdinalIgnoreCase))
        {
            return new((string?)null);
        }

        var token = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(part => part.Length == 2)
            .Where(part => string.Equals(part[0], "token", StringComparison.OrdinalIgnoreCase))
            .Select(part => Uri.UnescapeDataString(part[1]))
            .FirstOrDefault();

        return new(string.IsNullOrWhiteSpace(token) ? null : token);
    }
}