using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using Library.Extensions;

namespace Common.Tests.Extensions;

public class AuthenticationExtensionsTest
{
    // A hardcoded public-key PEM constant used to live here. It was dead code — declared, never
    // referenced, and a malformed key besides — and it was the only committed PEM payload in this
    // PUBLIC repository. Removed by F-016-T03 so AC-3 can be enforced literally rather than with a
    // carve-out. Tests below generate their keys at runtime via GenerateTestRsaPublicKeyPem().
    [Fact]
    public void AddAgendaBuddyAuthentication_WhenJwtPublicKeyEnvVarAbsent_ThrowsApplicationException()
    {
        // Given the JWT_PUBLIC_KEY environment variable is not set
        Environment.SetEnvironmentVariable("JWT_PUBLIC_KEY", null);
        var services = new ServiceCollection();
        services.AddLogging();

        // When AddAgendaBuddyAuthentication is called
        // Then an ApplicationException is thrown naming the missing env var
        var ex = Assert.Throws<ApplicationException>(
            () => services.AddAgendaBuddyAuthentication());
        Assert.Contains("JWT_PUBLIC_KEY", ex.Message);
    }

    [Fact]
    public void AddAgendaBuddyAuthentication_WhenJwtPublicKeyEnvVarEmpty_ThrowsApplicationException()
    {
        // Given the JWT_PUBLIC_KEY environment variable is empty
        Environment.SetEnvironmentVariable("JWT_PUBLIC_KEY", "");
        var services = new ServiceCollection();
        services.AddLogging();

        // When AddAgendaBuddyAuthentication is called
        // Then an ApplicationException is thrown
        var ex = Assert.Throws<ApplicationException>(
            () => services.AddAgendaBuddyAuthentication());
        Assert.Contains("JWT_PUBLIC_KEY", ex.Message);
    }

    [Fact]
    public async Task AddAgendaBuddyAuthentication_RegistersJwtBearerSchemeAsDefaultAuthScheme()
    {
        // Given a valid RSA public key PEM is set in JWT_PUBLIC_KEY
        Environment.SetEnvironmentVariable("JWT_PUBLIC_KEY", GenerateTestRsaPublicKeyPem());
        var services = new ServiceCollection();
        services.AddLogging();

        // When AddAgendaBuddyAuthentication is called
        services.AddAgendaBuddyAuthentication();
        var provider = services.BuildServiceProvider();

        // Then JWT Bearer is registered as the default authentication scheme
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var defaultScheme = await schemeProvider.GetDefaultAuthenticateSchemeAsync();
        Assert.NotNull(defaultScheme);
        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, defaultScheme.Name);
    }

    [Fact]
    public void AddAgendaBuddyAuthentication_ConfiguresValidAlgorithmsToRs256Only()
    {
        // Given a valid RSA public key PEM is set in JWT_PUBLIC_KEY
        Environment.SetEnvironmentVariable("JWT_PUBLIC_KEY", GenerateTestRsaPublicKeyPem());
        var services = new ServiceCollection();
        services.AddLogging();

        // When AddAgendaBuddyAuthentication is called
        services.AddAgendaBuddyAuthentication();
        var provider = services.BuildServiceProvider();

        // Then TokenValidationParameters has ValidAlgorithms pinned to RS256
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        Assert.NotNull(options.TokenValidationParameters.ValidAlgorithms);
        Assert.Contains("RS256", options.TokenValidationParameters.ValidAlgorithms);
        Assert.DoesNotContain("HS256", options.TokenValidationParameters.ValidAlgorithms ?? []);
        Assert.DoesNotContain("none", options.TokenValidationParameters.ValidAlgorithms ?? []);
    }

    [Fact]
    public void AddAgendaBuddyAuthentication_ConfiguresClockSkewToZero()
    {
        // Given a valid RSA public key PEM
        Environment.SetEnvironmentVariable("JWT_PUBLIC_KEY", GenerateTestRsaPublicKeyPem());
        var services = new ServiceCollection();
        services.AddLogging();

        // When AddAgendaBuddyAuthentication is called
        services.AddAgendaBuddyAuthentication();
        var provider = services.BuildServiceProvider();

        // Then ClockSkew is zero — no tolerance on token expiry
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        Assert.Equal(TimeSpan.Zero, options.TokenValidationParameters.ClockSkew);
    }

    [Fact]
    public void AddAgendaBuddyAuthentication_ConfiguresIssuerValidation()
    {
        // Given a valid RSA public key PEM
        Environment.SetEnvironmentVariable("JWT_PUBLIC_KEY", GenerateTestRsaPublicKeyPem());
        var services = new ServiceCollection();
        services.AddLogging();

        // When AddAgendaBuddyAuthentication is called
        services.AddAgendaBuddyAuthentication();
        var provider = services.BuildServiceProvider();

        // Then issuer validation is enabled with the correct issuer
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        Assert.True(options.TokenValidationParameters.ValidateIssuer);
        Assert.Equal("agenda-buddy-identity", options.TokenValidationParameters.ValidIssuer);
    }

    // Generates a real RSA key pair for testing purposes only
    private static string GenerateTestRsaPublicKeyPem()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var pubKeyBytes = rsa.ExportSubjectPublicKeyInfo();
        var b64 = Convert.ToBase64String(pubKeyBytes, Base64FormattingOptions.InsertLineBreaks);
        return $"-----BEGIN PUBLIC KEY-----\n{b64}\n-----END PUBLIC KEY-----";
    }
}
