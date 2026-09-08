namespace AgendaBuddy.Customer.Requests;

/// <summary>
/// Whether this account accepts the Terms and Conditions and the Privacy Policy.
/// </summary>
/// <remarks>
/// <para>
/// Two booleans rather than two dates: <b>the server stamps the time</b>. A consent record whose timestamp the
/// consenting party supplies proves nothing, and a device with a wrong clock would file the acceptance in the
/// wrong year.
/// </para>
/// <para>
/// Both are non-nullable and both are always written, so <c>false</c> means "not accepted" rather than
/// "unchanged". A partial-update shape would make an unticked box leave a previous acceptance standing, which is
/// the one outcome a consent record must never produce.
/// </para>
/// </remarks>
public record ConsentRequest(bool AcceptedTerms, bool AcceptedPrivacy);
