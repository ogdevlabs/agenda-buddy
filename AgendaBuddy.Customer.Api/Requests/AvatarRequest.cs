using System.ComponentModel.DataAnnotations;

namespace AgendaBuddy.Customer.Requests;

/// <summary>
/// Which built-in avatar to draw this account with.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>There is no email field, deliberately.</b> The account is the route's <c>{email}</c>, guarded against the
/// caller's own claim. A body that could name the account would be a body that could change somebody else's.
/// </para>
/// <para>
/// <c>[Required]</c> catches an absent or blank value at the boundary; whether the value names an avatar this
/// build actually ships is the handler's call, because it is a domain rule and the catalogue is the domain's.
/// </para>
/// </remarks>
public record AvatarRequest([Required] string AvatarId);
