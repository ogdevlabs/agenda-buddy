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
    [Required] string RefreshToken
);

public record TokenResponse(string AccessToken, string RefreshToken);

public record RegisterDeviceTokenRequest(
    [Required] string Token,
    [Required] string Platform
);
