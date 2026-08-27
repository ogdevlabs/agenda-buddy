using AgendaBuddy.Library.Tools;

namespace AgendaBuddy.Identity.Tests.Helpers;

public class FakeDateTimeProvider(DateTime utcNow) : IDateTimeProvider
{
    /// <summary>
    /// Settable so a test can pass a lock window without a second service instance (F-021 AC-8).
    /// </summary>
    /// <remarks>
    /// The lock is time-based and self-clearing, so "the window elapsed" has to be expressible as time
    /// passing rather than as a write. Before this was settable, the only way to move the clock was to
    /// build a second <c>IdentityService</c> over a second repository — which is how
    /// <c>Refresh_ExpiredToken_ThrowsUnauthorizedException</c> is written, and why it needs a comment
    /// apologising for itself.
    /// </remarks>
    public DateTime UtcNow { get; set; } = utcNow;

    public void Advance(TimeSpan by) => UtcNow += by;
}
