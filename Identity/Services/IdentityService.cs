using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Library.Entities;
using Library.Repositories;
using Library.Tools;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Identity.Services;

public class IdentityService(
    IRepository<CredentialEntity> repository,
    IDateTimeProvider clock)
{
    private const string PrivateKeyEnvVar = "JWT_PRIVATE_KEY";
    private const string Issuer = "agenda-buddy-identity";
    private static readonly string[] AllowedRoles = ["Provider", "Customer"];

    private static readonly string DummyHash =
        BCrypt.Net.BCrypt.HashPassword(Guid.Empty.ToString(), workFactor: 12);

    public async Task<TokenResponse?> RegisterAsync(string email, string password, string role)
    {
        email = email.ToLowerInvariant();

        if (password.Length < 8 || string.IsNullOrWhiteSpace(password))
            throw new AuthValidationException("Password must be at least 8 characters.");

        if (!AllowedRoles.Contains(role))
            throw new AuthValidationException("Role must be 'Provider' or 'Customer'.");

        var filter = new BsonDocument("email", email);

        CredentialEntity? existing;
        try
        {
            existing = await repository.FindOneAsync(filter);
        }
        catch (Exception ex) when (IsMongoDown(ex))
        {
            throw new ServiceUnavailableException();
        }

        if (existing is not null)
            throw new ConflictException("An account with this email already exists.");

        var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
        var (accessToken, refreshOpaque, refreshHash) = GenerateTokenPair(email, role);

        var credential = new CredentialEntity
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Email = email,
            PasswordHash = hash,
            Role = role,
            MustResetPassword = false,
            RefreshToken = new RefreshTokenDocument
            {
                Hash = refreshHash,
                Expiry = clock.UtcNow.AddHours(24)
            }
        };

        try
        {
            await repository.InsertAsync(credential);
        }
        catch (Exception ex) when (IsMongoDown(ex))
        {
            throw new ServiceUnavailableException();
        }

        return new TokenResponse(accessToken, refreshOpaque);
    }

    public async Task<TokenResponse?> LoginAsync(string email, string password)
    {
        email = email.ToLowerInvariant();

        CredentialEntity? credential;
        try
        {
            credential = await repository.FindOneAsync(new BsonDocument("email", email));
        }
        catch (Exception ex) when (IsMongoDown(ex))
        {
            throw new ServiceUnavailableException();
        }

        if (credential is null)
        {
            // Constant-time dummy hash to prevent timing-based user enumeration (T-005)
            BCrypt.Net.BCrypt.Verify(password, DummyHash);
            throw new UnauthorizedException();
        }

        if (!BCrypt.Net.BCrypt.Verify(password, credential.PasswordHash))
            throw new UnauthorizedException();

        var (accessToken, refreshOpaque, refreshHash) = GenerateTokenPair(email, credential.Role);

        credential.RefreshToken = new RefreshTokenDocument
        {
            Hash = refreshHash,
            Expiry = clock.UtcNow.AddHours(24)
        };

        try
        {
            await repository.UpdateAsync(credential.Id, credential);
        }
        catch (Exception ex) when (IsMongoDown(ex))
        {
            throw new ServiceUnavailableException();
        }

        return new TokenResponse(accessToken, refreshOpaque);
    }

    public async Task<TokenResponse?> RefreshAsync(string refreshToken)
    {
        var hash = HashToken(refreshToken);

        CredentialEntity? credential;
        try
        {
            var filter = new BsonDocument
            {
                { "refresh_token.hash", hash },
                { "refresh_token.expiry", new BsonDocument("$gt", clock.UtcNow) }
            };
            credential = await repository.FindOneAndDeleteAsync(filter);
        }
        catch (Exception ex) when (IsMongoDown(ex))
        {
            throw new ServiceUnavailableException();
        }

        if (credential is null)
            throw new UnauthorizedException("Refresh token is invalid or expired.");

        var (accessToken, refreshOpaque, refreshHash) = GenerateTokenPair(credential.Email, credential.Role);

        credential.RefreshToken = new RefreshTokenDocument
        {
            Hash = refreshHash,
            Expiry = clock.UtcNow.AddHours(24)
        };

        try
        {
            await repository.InsertAsync(credential);
        }
        catch (Exception ex) when (IsMongoDown(ex))
        {
            throw new ServiceUnavailableException();
        }

        return new TokenResponse(accessToken, refreshOpaque);
    }

    public async Task LogoutAsync(string refreshToken)
    {
        var hash = HashToken(refreshToken);

        try
        {
            var filter = new BsonDocument("refresh_token.hash", hash);
            // Idempotent — UpdateAsync where hash matches, unset the sub-document
            var credential = await repository.FindOneAsync(filter);
            if (credential is not null)
            {
                credential.RefreshToken = null;
                await repository.UpdateAsync(credential.Id, credential);
            }
        }
        catch (Exception ex) when (IsMongoDown(ex))
        {
            throw new ServiceUnavailableException();
        }
    }

    private (string accessToken, string refreshOpaque, string refreshHash) GenerateTokenPair(
        string email, string role)
    {
        var privateKeyPem = Environment.GetEnvironmentVariable(PrivateKeyEnvVar)
            ?? throw new ApplicationException(
                $"Required environment variable '{PrivateKeyEnvVar}' is not set.");

        var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        var signingKey = new RsaSecurityKey(rsa);
        var signingCreds = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, email),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            claims: claims,
            expires: clock.UtcNow.AddMinutes(60),
            signingCredentials: signingCreds);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        var refreshOpaque = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var refreshHash = HashToken(refreshOpaque);

        return (accessToken, refreshOpaque, refreshHash);
    }

    public static string HashToken(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool IsMongoDown(Exception ex) =>
        ex is MongoConnectionException or MongoException or TimeoutException;
}

public record TokenResponse(string AccessToken, string RefreshToken);

public class AuthValidationException(string message) : Exception(message);
public class ConflictException(string message) : Exception(message);
public class UnauthorizedException(string message = "Invalid credentials.") : Exception(message);
public class ServiceUnavailableException() : Exception("Authentication service temporarily unavailable.");
