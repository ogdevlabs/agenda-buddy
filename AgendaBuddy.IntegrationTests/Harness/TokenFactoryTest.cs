using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AgendaBuddy.Library.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// Pins <see cref="TokenFactory"/> — F-016-T05, the token half of AC-6.
/// </summary>
/// <remarks>
/// <para>
/// Every token here is validated against the <b>services' own</b>
/// <see cref="TokenValidationParameters"/>, obtained by calling
/// <c>AddAgendaBuddyAuthentication()</c> and reading back the configured
/// <see cref="JwtBearerOptions"/> — the same technique
/// <c>Library.Tests/Extensions/AuthenticationExtensionsTest.cs</c> uses. Hand-copying the parameters
/// into the test would let issuer, algorithm or clock-skew drift apart from production, and the
/// symptom would be an unexplained 401 in F-016-T07 with nothing pointing at the cause.
/// </para>
/// <para>
/// This class mutates the process-wide <c>JWT_PUBLIC_KEY</c>, which is why it is in
/// <see cref="HarnessCollection"/>.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class TokenFactoryTest
{
    private const string Owner = "owner@example.com";
    private const string Stranger = "stranger@example.com";

    private readonly TokenFactory _tokens;
    private readonly TokenValidationParameters _productionParameters;

    public TokenFactoryTest(CryptoSessionFixture crypto)
    {
        _tokens = new TokenFactory(crypto);
        _productionParameters = ResolveProductionValidationParameters(crypto);
    }

    /// <summary>
    /// The validation parameters a real service would use, built by the production extension method
    /// against this session's public key.
    /// </summary>
    private static TokenValidationParameters ResolveProductionValidationParameters(CryptoSessionFixture crypto)
    {
        Environment.SetEnvironmentVariable("JWT_PUBLIC_KEY", crypto.PublicKeyPem);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAgendaBuddyAuthentication();

        return services.BuildServiceProvider()
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme)
            .TokenValidationParameters;
    }

    private ClaimsPrincipal Validate(string token) =>
        new JwtSecurityTokenHandler().ValidateToken(token, _productionParameters, out _);

    [Fact]
    public void ValidToken_PassesTheServicesOwnValidationParameters()
    {
        var principal = Validate(_tokens.CreateToken(Owner, TokenFactory.ProviderRole));

        // `sub` arrives as NameIdentifier because the JWT handler's inbound claim-type map renames it.
        // That is exactly what OwnershipGuard reads (Library.ServerAuth/Tools/OwnershipGuard.cs:9,16),
        // so this assertion is what makes ownership checks work against harness tokens at all.
        Assert.Equal(Owner, principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.True(principal.IsInRole(TokenFactory.ProviderRole));
    }

    [Fact]
    public void ExpiredToken_IsRejectedAsExpired()
    {
        // ClockSkew is TimeSpan.Zero in production (AuthenticationExtensions.cs:42), so there is no
        // grace window to work around and no need to date the token far in the past.
        var expired = _tokens.CreateExpiredToken(Owner);

        Assert.Throws<SecurityTokenExpiredException>(() => Validate(expired));
    }

    [Fact]
    public void TokenForAnotherSubject_IsPerfectlyValid_AndCarriesTheOtherSubject()
    {
        // The "foreign subject" token AC-6 calls for needs no dedicated factory method — it is simply
        // a valid token for somebody else. Recorded as a test so the absence of a
        // CreateForeignSubjectToken() reads as a decision rather than an omission. The 403 this
        // produces against a real route is F-016-T07's assertion, not this task's.
        var principal = Validate(_tokens.CreateToken(Stranger));

        Assert.Equal(Stranger, principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.NotEqual(Owner, principal.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    [Fact]
    public void TokenWithoutSubject_ValidatesButCarriesNoNameIdentifier()
    {
        // The precondition for threat T-001. A token with no `sub` is perfectly well-formed and passes
        // signature, issuer and lifetime validation — it simply has no NameIdentifier. That is what
        // makes OwnershipGuard.AssertOwner's null-claim path reachable over HTTP rather than
        // theoretical, and F-016-T09 is where the hole itself gets closed and asserted (AC-21).
        var principal = Validate(_tokens.CreateTokenWithoutSubject());

        Assert.Null(principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.True(principal.IsInRole(TokenFactory.ProviderRole));
    }
}
