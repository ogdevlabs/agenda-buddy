using System.Security.Claims;

namespace Library.Tools;

public static class OwnershipGuard
{
    /// <summary>
    /// Throws unless the caller's <c>sub</c> claim is <paramref name="entityEmail"/>.
    /// </summary>
    /// <param name="entityEmail">
    /// The owning email. <b>Nullable on purpose</b> — callers pass entity fields such as
    /// <c>ProviderEntity.Email</c>, which are themselves nullable. Declaring it non-nullable did not make
    /// nulls impossible; it only stopped the compiler from asking what happens to them.
    /// </param>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Both sides are null-guarded before comparing, and that is the entire point (F-016-T09,
    /// threat T-001, PRD AC-21).</b> This method previously called
    /// <c>string.Equals(sub, entityEmail, OrdinalIgnoreCase)</c> with no null check. Since
    /// <c>string.Equals(null, null)</c> is <c>true</c>, a caller carrying <b>no <c>sub</c> claim</b>,
    /// checked against an entity with <b>no email</b>, was granted <b>ownership</b> — the guard's
    /// most permissive outcome reached by its least-authenticated input.
    /// </para>
    /// <para>
    /// <see cref="AssertOwnerAny"/> has always guarded <c>sub is null</c> first. That asymmetry was the
    /// defect, and <c>T001_AssertOwnerAndAssertOwnerAny_NowAgreeOnAMissingSubClaim</c> keeps the two from
    /// diverging again.
    /// </para>
    /// <para>
    /// An entity with no email has no owner, so no caller may be treated as its owner — not even a
    /// fully authenticated one. That is why <paramref name="entityEmail"/> being null is refused
    /// independently of the claim.
    /// </para>
    /// </remarks>
    public static void AssertOwner(ClaimsPrincipal user, string? entityEmail)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (sub is null
            || entityEmail is null
            || !string.Equals(sub, entityEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException();
        }
    }

    public static void AssertOwnerAny(ClaimsPrincipal user, params string[] entityEmails)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (sub is null || !entityEmails.Any(e => string.Equals(sub, e, StringComparison.OrdinalIgnoreCase)))
            throw new ForbiddenException();
    }

    public static void AssertRole(ClaimsPrincipal user, string requiredRole)
    {
        if (!user.IsInRole(requiredRole))
            throw new ForbiddenException();
    }
}

public class ForbiddenException : Exception
{
    public int StatusCode => 403;

    public ForbiddenException()
        : base("You do not have permission to perform this action.") { }

    public ForbiddenException(string message)
        : base(message) { }
}
