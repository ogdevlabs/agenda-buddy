using System.ComponentModel.DataAnnotations;

namespace AgendaBuddy.Identity.Requests;

public record RegisterRequest(
    [Required][EmailAddress] string Email,
    [Required] string Password,
    [Required] string Role
);

public record LoginRequest(
    [Required][EmailAddress] string Email,
    [Required] string Password
);

public record RefreshRequest(
    [Required] string RefreshToken
);

public record LogoutRequest(
    [Required] string RefreshToken,
    string? AccessToken = null
);

/// <summary>
/// <paramref name="EmailVerificationToken"/> is the raw opaque email-confirmation token, present only on
/// a fresh registration — for tests and local-dev logging only, mirroring
/// <see cref="Services.IdentityService.RequestPasswordResetAsync"/>'s own remarks. The HTTP endpoint
/// projects this out before responding; it must never reach the wire.
/// </summary>
public record TokenResponse(string AccessToken, string RefreshToken, string? EmailVerificationToken = null);

public record RegisterDeviceTokenRequest(
    [Required] string Token,
    [Required] string Platform
);

public record PasswordResetRequestRequest(
    [Required][EmailAddress] string Email
);

public record PasswordResetConfirmRequest(
    [Required][EmailAddress] string Email,
    [Required] string Token,
    [Required] string NewPassword
);

public record EmailConfirmRequest(
    [Required][EmailAddress] string Email,
    [Required] string Token
);
