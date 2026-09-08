using System.ComponentModel.DataAnnotations;

namespace AgendaBuddy.Provider.Domain.Requests;

/// <summary>
/// Which built-in avatar to draw this account with.
/// </summary>
/// <remarks>
/// A byte-identical twin of Customer's own <c>AvatarRequest</c>, and deliberately not a shared type — no
/// cross-service code needs the same type here, only the same shape, the same reasoning that keeps each service's
/// <c>DataResponse&lt;T&gt;</c> its own (see <c>docs/pdlc/design/api-refactor-rollout/ARCHITECTURE.md</c> §3).
/// <para>
/// ⚠️ <b>There is no email field.</b> The account is the route's <c>{email}</c>, guarded against the caller's own
/// claim.
/// </para>
/// </remarks>
public record AvatarRequest([Required] string AvatarId);
