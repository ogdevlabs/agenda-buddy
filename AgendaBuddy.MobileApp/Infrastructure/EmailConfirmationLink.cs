namespace AgendaBuddy.MobileApp.Infrastructure;

public sealed record EmailConfirmationLink(string? Token)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(Token);

    public static EmailConfirmationLink Parse(Uri uri)
    {
        var verifiedWebLink = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.Host, "agendame.app", StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.AbsolutePath.Trim('/'), "confirm-email", StringComparison.OrdinalIgnoreCase);
#if DEBUG
        var localSimulatorLink = string.Equals(uri.Scheme, "agendame", StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.Host, "email", StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.AbsolutePath.Trim('/'), "confirm-email", StringComparison.OrdinalIgnoreCase);
#else
        const bool localSimulatorLink = false;
#endif
        if (!verifiedWebLink && !localSimulatorLink)
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