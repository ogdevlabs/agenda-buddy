using System.Diagnostics;
using System.Text.RegularExpressions;
using OpenTelemetry;

namespace AgendaBuddy.ServiceDefaults;

/// <summary>
/// Strips email addresses out of span tags before they are exported.
/// </summary>
/// <remarks>
/// The threat model assumed ASP.NET Core instrumentation records route templates rather than raw
/// paths. That is true of <c>http.route</c> but **not** of <c>url.path</c>, which carries the
/// literal request path — and this system puts email addresses in paths
/// (<c>GET /api/v1/providers/{email}</c>). Without this processor, every request to a
/// provider-by-email or customer-by-email endpoint exports the address to whatever collector is
/// configured. `CONSTITUTION.md` §4 treats email as the PII of record.
/// <para>
/// Redacting rather than dropping the tag keeps the span useful for debugging: the shape of the
/// path survives, only the identity is removed.
/// </para>
/// </remarks>
internal sealed partial class PiiRedactingProcessor : BaseProcessor<Activity>
{
    /// <summary>Tags known to carry a raw URL, and therefore any PII embedded in one.</summary>
    private static readonly string[] UrlTags =
    [
        "url.path",
        "url.query",
        "url.full",
        "http.url",
        "http.target"
    ];

    private const string Replacement = "[redacted-email]";

    /// <summary>
    /// Matches an email address inside a URL path or query. Deliberately narrow: it must contain
    /// an <c>@</c> with non-delimiter characters either side.
    /// </summary>
    [GeneratedRegex(@"[^/?&=\s@]+@[^/?&=\s@]+\.[^/?&=\s@]+", RegexOptions.IgnoreCase)]
    private static partial Regex EmailPattern();

    /// <summary>
    /// Redacts email addresses from URL-bearing tags as the span ends, before any exporter sees it.
    /// </summary>
    /// <param name="data">The activity being ended.</param>
    public override void OnEnd(Activity data)
    {
        if (data is null) return;

        foreach (var tag in UrlTags)
        {
            if (data.GetTagItem(tag) is not string value || value.Length == 0) continue;

            var redacted = EmailPattern().Replace(value, Replacement);
            if (!ReferenceEquals(redacted, value) && redacted != value) data.SetTag(tag, redacted);
        }

        // The span's display name is the route template for ASP.NET Core spans, but a custom span
        // could carry a raw path — cheap to cover.
        if (EmailPattern().IsMatch(data.DisplayName))
        {
            data.DisplayName = EmailPattern().Replace(data.DisplayName, Replacement);
        }
    }
}
