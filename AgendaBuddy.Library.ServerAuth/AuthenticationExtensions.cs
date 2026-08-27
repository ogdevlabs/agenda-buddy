using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace AgendaBuddy.Library.Extensions;

public static class AuthenticationExtensions
{
    private const string Issuer = "agenda-buddy-identity";
    private const string PublicKeyEnvVar = "JWT_PUBLIC_KEY";

    public static IServiceCollection AddAgendaBuddyAuthentication(this IServiceCollection services)
    {
        var publicKeyPem = Environment.GetEnvironmentVariable(PublicKeyEnvVar);

        if (string.IsNullOrWhiteSpace(publicKeyPem))
            throw new ApplicationException(
                $"Required environment variable '{PublicKeyEnvVar}' is not set. " +
                "Provide the RSA public key in PEM format before starting the service.");

        publicKeyPem = publicKeyPem.Replace("\\n", "\n");

        var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);
        var rsaKey = new RsaSecurityKey(rsa);

        // Log key fingerprint at startup — key material never logged, only SHA-256 of the public key bytes
        LogKeyFingerprint(services, rsaKey);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = Issuer,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = rsaKey,
                    ValidAlgorithms = ["RS256"],
                };
            });

        return services;
    }

    private static void LogKeyFingerprint(IServiceCollection services, RsaSecurityKey rsaKey)
    {
        var sp = services.BuildServiceProvider();
        var loggerFactory = sp.GetService<ILoggerFactory>();
        if (loggerFactory is null) return;

        var logger = loggerFactory.CreateLogger(nameof(AuthenticationExtensions));
        var pubKeyBytes = rsaKey.Rsa.ExportSubjectPublicKeyInfo();
        var fingerprint = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(pubKeyBytes)).ToLowerInvariant();
        logger.LogInformation("RSA public key loaded (fingerprint: {Fingerprint})", fingerprint);
    }
}
