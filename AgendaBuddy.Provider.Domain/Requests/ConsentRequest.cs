namespace AgendaBuddy.Provider.Domain.Requests;

/// <summary>
/// Whether this account accepts the Terms and Conditions and the Privacy Policy. Twin of Customer's own
/// <c>ConsentRequest</c>, for the reason given on <see cref="AvatarRequest"/>.
/// </summary>
/// <remarks>
/// Two booleans rather than two dates: <b>the server stamps the time</b>, because a consent record whose
/// timestamp the consenting party supplies proves nothing. Both are always written, so <c>false</c> means "not
/// accepted" and not "unchanged".
/// </remarks>
public record ConsentRequest(bool AcceptedTerms, bool AcceptedPrivacy);
