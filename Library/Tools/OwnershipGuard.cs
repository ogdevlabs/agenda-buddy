using System.Security.Claims;

namespace Library.Tools;

public static class OwnershipGuard
{
    public static void AssertOwner(ClaimsPrincipal user, string entityEmail)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.Equals(sub, entityEmail, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException();
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
