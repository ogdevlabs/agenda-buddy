using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AgendaBuddy.Identity.Services;
using AgendaBuddy.Identity.Tests.Helpers;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace AgendaBuddy.Identity.Tests.Services;

[Collection("Sequential")]
public class IdentityServiceTest : IDisposable
{
    private readonly string _publicKeyPem;
    private readonly string _privateKeyPem;
    private readonly FakeDateTimeProvider _clock;
    private readonly InMemoryCredentialRepository _repo;
    private readonly IdentityService _svc;

    public IdentityServiceTest()
    {
        (_publicKeyPem, _privateKeyPem) = RsaKeyHelper.GenerateTestKeyPair();
        Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", _privateKeyPem);
        _clock = new FakeDateTimeProvider(new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc));
        _repo = new InMemoryCredentialRepository();
        _svc = new IdentityService(_repo, _clock);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", null);
        Environment.SetEnvironmentVariable("JWT_PUBLIC_KEY", null);
    }

    // --- Register ---

    [Fact]
    public async Task Register_ValidRequest_ReturnsTokenPair()
    {
        var result = await _svc.RegisterAsync("user@example.com", "password123", "Provider");

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
    }

    [Fact]
    public async Task Register_NormalizesEmailToLowercase()
    {
        await _svc.RegisterAsync("USER@Example.COM", "password123", "Provider");

        var all = await _repo.GetAllAsync();
        Assert.Contains(all, e => e.Email == "user@example.com");
    }

    [Fact]
    public async Task Register_PasswordTooShort_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<AuthValidationException>(() =>
            _svc.RegisterAsync("user@example.com", "short", "Provider"));
    }

    [Fact]
    public async Task Register_InvalidRole_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<AuthValidationException>(() =>
            _svc.RegisterAsync("user@example.com", "password123", "Admin"));
    }

    [Fact]
    public async Task Register_DuplicateEmail_ThrowsConflictException()
    {
        await _svc.RegisterAsync("dup@example.com", "password123", "Provider");
        await Assert.ThrowsAsync<ConflictException>(() =>
            _svc.RegisterAsync("dup@example.com", "password456", "Customer"));
    }

    [Fact]
    public async Task Register_AccessToken_HasCorrectClaims()
    {
        var result = await _svc.RegisterAsync("claims@example.com", "password123", "Provider");

        var claims = DecodeToken(result!.AccessToken);
        Assert.Equal("claims@example.com", GetClaim(claims, JwtRegisteredClaimNames.Sub));
        Assert.Equal("agenda-buddy-identity", GetClaim(claims, JwtRegisteredClaimNames.Iss));
        Assert.False(string.IsNullOrWhiteSpace(GetClaim(claims, JwtRegisteredClaimNames.Jti)));
    }

    [Fact]
    public async Task Register_AccessToken_SignedWithRs256()
    {
        var result = await _svc.RegisterAsync("rsa@example.com", "password123", "Provider");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result!.AccessToken);
        Assert.Equal("RS256", jwt.Header.Alg);
    }

    [Fact]
    public async Task Register_AccessToken_Expires60MinFromClock()
    {
        var result = await _svc.RegisterAsync("exp@example.com", "password123", "Provider");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result!.AccessToken);
        var expectedExp = _clock.UtcNow.AddMinutes(60);
        Assert.Equal(expectedExp, jwt.ValidTo, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Register_RefreshToken_StoredAsHash_NotPlaintext()
    {
        var result = await _svc.RegisterAsync("hash@example.com", "password123", "Provider");

        var all = await _repo.GetAllAsync();
        var stored = System.Linq.Enumerable.First(all, e => e.Email == "hash@example.com");

        // The stored hash must NOT equal the opaque token
        Assert.NotEqual(result!.RefreshToken, stored.RefreshToken!.Hash);
        // But SHA-256 of the opaque token must match the stored hash
        var expectedHash = IdentityService.HashToken(result.RefreshToken);
        Assert.Equal(expectedHash, stored.RefreshToken.Hash);
    }

    [Fact]
    public async Task Register_RefreshToken_ExpiresIn24Hours()
    {
        await _svc.RegisterAsync("ttl@example.com", "password123", "Provider");

        var all = await _repo.GetAllAsync();
        var stored = System.Linq.Enumerable.First(all, e => e.Email == "ttl@example.com");
        Assert.Equal(_clock.UtcNow.AddHours(24), stored.RefreshToken!.Expiry);
    }

    [Fact]
    public async Task Register_PasswordStoredAsBcryptHash_NotPlaintext()
    {
        await _svc.RegisterAsync("bcrypt@example.com", "mypassword123", "Provider");

        var all = await _repo.GetAllAsync();
        var stored = System.Linq.Enumerable.First(all, e => e.Email == "bcrypt@example.com");

        Assert.NotEqual("mypassword123", stored.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("mypassword123", stored.PasswordHash));
    }

    // --- Login ---

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokenPair()
    {
        await _svc.RegisterAsync("login@example.com", "password123", "Provider");
        var result = await _svc.LoginAsync("login@example.com", "password123");

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
    }

    [Fact]
    public async Task Login_WrongPassword_ThrowsUnauthorizedException()
    {
        await _svc.RegisterAsync("login2@example.com", "password123", "Provider");
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _svc.LoginAsync("login2@example.com", "wrongpassword"));
    }

    [Fact]
    public async Task Login_UnknownEmail_ThrowsUnauthorizedException()
    {
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _svc.LoginAsync("nobody@example.com", "password123"));
    }

    // --- Refresh ---

    [Fact]
    public async Task Refresh_ValidToken_ReturnsNewTokenPair()
    {
        var reg = await _svc.RegisterAsync("refresh@example.com", "password123", "Provider");
        var result = await _svc.RefreshAsync(reg!.RefreshToken);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.NotEqual(reg.RefreshToken, result.RefreshToken);
    }

    [Fact]
    public async Task Refresh_TokenUsedTwice_SecondCallThrowsUnauthorizedException()
    {
        var reg = await _svc.RegisterAsync("refresh2@example.com", "password123", "Provider");
        await _svc.RefreshAsync(reg!.RefreshToken);
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _svc.RefreshAsync(reg.RefreshToken));
    }

    [Fact]
    public async Task Refresh_ExpiredToken_ThrowsUnauthorizedException()
    {
        var reg = await _svc.RegisterAsync("refresh3@example.com", "password123", "Provider");

        // Advance clock past expiry
        var futureRepo = new InMemoryCredentialRepository();
        await futureRepo.InsertAsync((await _repo.GetAllAsync()).First(e => e.Email == "refresh3@example.com"));
        var futureClock = new FakeDateTimeProvider(_clock.UtcNow.AddHours(25));
        var futureSvc = new IdentityService(futureRepo, futureClock);

        // Re-insert the credential with our base repo state
        // (Simpler: just use an expired sub-doc directly)
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            futureSvc.RefreshAsync(reg!.RefreshToken));
    }

    // --- Logout ---

    [Fact]
    public async Task Logout_ValidToken_SetsRefreshTokenToNull()
    {
        var reg = await _svc.RegisterAsync("logout@example.com", "password123", "Provider");
        await _svc.LogoutAsync(reg!.RefreshToken);

        var all = await _repo.GetAllAsync();
        var stored = System.Linq.Enumerable.First(all, e => e.Email == "logout@example.com");
        Assert.Null(stored.RefreshToken);
    }

    [Fact]
    public async Task Logout_AlreadyLoggedOut_DoesNotThrow()
    {
        var reg = await _svc.RegisterAsync("logout2@example.com", "password123", "Provider");
        await _svc.LogoutAsync(reg!.RefreshToken);
        // Second logout must be idempotent
        await _svc.LogoutAsync(reg.RefreshToken);
    }

    // --- HashToken ---

    [Fact]
    public void HashToken_IsDeterministic()
    {
        var h1 = IdentityService.HashToken("test-token");
        var h2 = IdentityService.HashToken("test-token");
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void HashToken_DifferentInputs_ProduceDifferentHashes()
    {
        var h1 = IdentityService.HashToken("token-a");
        var h2 = IdentityService.HashToken("token-b");
        Assert.NotEqual(h1, h2);
    }

    // Helpers

    private static System.Collections.Generic.IEnumerable<Claim> DecodeToken(string jwt)
    {
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(jwt);
        return token.Claims;
    }

    private static string? GetClaim(System.Collections.Generic.IEnumerable<Claim> claims, string type)
        => claims.FirstOrDefault(c => c.Type == type)?.Value;
}
