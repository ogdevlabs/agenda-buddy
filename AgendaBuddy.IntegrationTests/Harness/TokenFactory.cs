using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// Mints RS256 access tokens with the session keypair, in the exact shape a real service accepts.
/// </summary>
/// <remarks>
/// <para>
/// F-016-T05 / AC-6. Deliberately mirrors <c>Identity/Services/IdentityService.cs:200-213</c> claim for
/// claim — issuer <c>agenda-buddy-identity</c>, <c>sub</c>, <c>role</c>, <c>jti</c>, RS256, no
/// audience. A harness token that differs from a production token in any of those validates
/// differently, and the symptom is a 401 that looks like an authorization bug in the code under test
/// rather than a defect in the test fixture.
/// </para>
/// <para>
/// <b>There is no <c>CreateForeignSubjectToken</c>.</b> The "valid signature, different subject" token
/// AC-6 asks for is just <see cref="CreateToken"/> for somebody else, so adding a method for it would
/// be naming a parameter value. <c>TokenFactoryTest</c> pins that as a decision rather than an
/// oversight.
/// </para>
/// <para>
/// Signing uses <see cref="CryptoSessionFixture.SigningKey"/> — the live key — because the fixture
/// deliberately never materialises a private-key PEM string. See <see cref="CryptoSessionFixture"/>.
/// </para>
/// </remarks>
internal sealed class TokenFactory(CryptoSessionFixture crypto)
{
    /// <summary>Matches <c>IdentityService.AllowedRoles</c>, which is a bare string array.</summary>
    public const string ProviderRole = "Provider";

    /// <inheritdoc cref="ProviderRole"/>
    public const string CustomerRole = "Customer";

    private const string Issuer = "agenda-buddy-identity";

    private readonly SigningCredentials _signingCredentials =
        new(new RsaSecurityKey(crypto.SigningKey), SecurityAlgorithms.RsaSha256);

    /// <summary>A valid token for <paramref name="subject"/>, expiring in an hour.</summary>
    public string CreateToken(string subject, string role = ProviderRole) =>
        Write(Claims(role, subject), DateTime.UtcNow.AddHours(1));

    /// <summary>
    /// A token identical to <see cref="CreateToken"/> but already expired.
    /// </summary>
    /// <remarks>
    /// One minute in the past is ample: production sets <c>ClockSkew = TimeSpan.Zero</c>
    /// (<c>AuthenticationExtensions.cs:42</c>), so there is no grace window to outrun.
    /// </remarks>
    public string CreateExpiredToken(string subject, string role = ProviderRole) =>
        Write(Claims(role, subject), DateTime.UtcNow.AddMinutes(-1));

    /// <summary>
    /// A valid, correctly signed, unexpired token carrying <b>no</b> <c>sub</c> claim.
    /// </summary>
    /// <remarks>
    /// The precondition for threat <b>T-001</b>: such a token authenticates successfully and then
    /// presents a null <c>NameIdentifier</c> to <c>OwnershipGuard.AssertOwner</c>, whose null-claim
    /// path currently falls through to the <em>owner</em> branch. F-016-T09 closes that (AC-21); this
    /// method is what lets the closure be proven over real HTTP instead of argued about.
    /// </remarks>
    public string CreateTokenWithoutSubject(string role = ProviderRole) =>
        Write(Claims(role, subject: null), DateTime.UtcNow.AddHours(1));

    private static Claim[] Claims(string role, string? subject)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        if (subject is not null)
        {
            claims.Insert(0, new Claim(JwtRegisteredClaimNames.Sub, subject));
        }

        return [.. claims];
    }

    private string Write(Claim[] claims, DateTime expires) =>
        new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(
                issuer: Issuer,
                claims: claims,
                expires: expires,
                signingCredentials: _signingCredentials));
}
