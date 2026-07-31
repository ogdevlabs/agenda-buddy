using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Identity.Tests.Helpers;
using Library.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Identity.Tests.Auth;

[Collection("Sequential")]
public class JwtMiddlewareMatrixTest : IDisposable
{
    private readonly string _publicKeyPem;
    private readonly string _privateKeyPem;
    private readonly TokenValidationParameters _validationParams;

    public JwtMiddlewareMatrixTest()
    {
        (_publicKeyPem, _privateKeyPem) = RsaKeyHelper.GenerateTestKeyPair();
        Environment.SetEnvironmentVariable("JWT_PUBLIC_KEY", _publicKeyPem);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAgendaBuddyAuthentication();
        var provider = services.BuildServiceProvider();

        var options = provider
            .GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        _validationParams = options.TokenValidationParameters;
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JWT_PUBLIC_KEY", null);
        Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", null);
    }

    [Fact]
    public void ValidToken_PassesValidation()
    {
        var token = IssueToken("user@example.com", "Provider", expired: false);
        Assert.True(TryValidate(token, out _));
    }

    [Fact]
    public void ExpiredToken_FailsValidation()
    {
        var token = IssueToken("user@example.com", "Provider", expired: true);
        Assert.False(TryValidate(token, out _));
    }

    [Fact]
    public void TokenWithWrongIssuer_FailsValidation()
    {
        var token = IssueToken("user@example.com", "Provider", expired: false, issuer: "evil-issuer");
        Assert.False(TryValidate(token, out _));
    }

    [Fact]
    public void TokenSignedWithHS256_FailsValidation()
    {
        // Threat-model T-003: algorithm confusion attack
        var hmacKey = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64));
        var creds = new SigningCredentials(hmacKey, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            issuer: "agenda-buddy-identity",
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, "user@example.com") },
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: creds);
        var token = new JwtSecurityTokenHandler().WriteToken(jwt);

        Assert.False(TryValidate(token, out _));
    }

    [Fact]
    public void TokenWithAlgNone_FailsValidation()
    {
        // Construct unsigned token manually
        var header = Base64UrlEncoder.Encode("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var payload = Base64UrlEncoder.Encode("{\"sub\":\"user@example.com\",\"iss\":\"agenda-buddy-identity\"}");
        var unsignedToken = $"{header}.{payload}.";

        Assert.False(TryValidate(unsignedToken, out _));
    }

    [Fact]
    public void ValidAlgorithms_ContainsOnlyRs256()
    {
        var algs = _validationParams.ValidAlgorithms?.ToList();
        Assert.NotNull(algs);
        Assert.Single(algs);
        Assert.Contains("RS256", algs);
    }

    [Fact]
    public void ClockSkew_IsZero()
    {
        Assert.Equal(TimeSpan.Zero, _validationParams.ClockSkew);
    }

    [Fact]
    public void ValidIssuer_IsAgendaBuddyIdentity()
    {
        Assert.Equal("agenda-buddy-identity", _validationParams.ValidIssuer);
    }

    [Fact]
    public void ValidateIssuer_IsTrue()
    {
        Assert.True(_validationParams.ValidateIssuer);
    }

    // Token with RS256 but signed by a different key pair
    [Fact]
    public void TokenSignedWithDifferentKey_FailsValidation()
    {
        // Generate a separate RSA key and sign directly — no env var mutation
        var (_, otherPrivateKey) = RsaKeyHelper.GenerateTestKeyPair();
        var rsa = RSA.Create();
        rsa.ImportFromPem(otherPrivateKey);
        var key = new RsaSecurityKey(rsa);
        var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
        var jwt = new JwtSecurityToken(
            issuer: "agenda-buddy-identity",
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, "user@example.com") },
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: creds);
        var token = new JwtSecurityTokenHandler().WriteToken(jwt);

        Assert.False(TryValidate(token, out _));
    }

    private string IssueToken(
        string email,
        string role,
        bool expired,
        string issuer = "agenda-buddy-identity")
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(_privateKeyPem);
        var key = new RsaSecurityKey(rsa);
        var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        var now = DateTime.UtcNow;
        var exp = expired ? now.AddMinutes(-5) : now.AddMinutes(60);

        var jwt = new JwtSecurityToken(
            issuer: issuer,
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, email),
                new Claim(ClaimTypes.Role, role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            },
            notBefore: expired ? now.AddMinutes(-10) : now,
            expires: exp,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private bool TryValidate(string token, out ClaimsPrincipal? principal)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            principal = handler.ValidateToken(token, _validationParams, out _);
            return true;
        }
        catch
        {
            principal = null;
            return false;
        }
    }
}
