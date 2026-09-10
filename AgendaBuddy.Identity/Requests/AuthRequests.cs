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

public record TokenResponse(string AccessToken, string RefreshToken);

/// <summary>
/// Service-level registration result. The raw token exists only so tests can exercise confirmation; the
/// HTTP endpoint must never project it onto the wire.
/// </summary>
public record RegistrationResponse(string EmailVerificationToken);

public record RegisterDeviceTokenRequest(
    [Required] string Token,
    [Required] string Platform,
    string? LanguageCode = null
);

public record PasswordResetRequestRequest(
    [Required][EmailAddress] string Email
);

public record PasswordResetConfirmRequest(
    [Required][EmailAddress] string Email,
    [Required] string Token,
    [Required] string NewPassword
);

public record EmailConfirmRequest([Required] string Token);

public record EmailVerificationRequest([Required][EmailAddress] string Email);
