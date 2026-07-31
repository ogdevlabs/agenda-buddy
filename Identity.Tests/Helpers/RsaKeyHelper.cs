using System.Security.Cryptography;

namespace Identity.Tests.Helpers;

/// <summary>
/// Generates ephemeral RSA key pairs for test use only.
/// Keys are created fresh per test run and never persisted.
/// </summary>
public static class RsaKeyHelper
{
    public static (string publicKeyPem, string privateKeyPem) GenerateTestKeyPair()
    {
        using var rsa = RSA.Create(2048);
        var pubBytes = rsa.ExportSubjectPublicKeyInfo();
        var prvBytes = rsa.ExportPkcs8PrivateKey();
        var pub64 = Convert.ToBase64String(pubBytes, Base64FormattingOptions.InsertLineBreaks);
        var prv64 = Convert.ToBase64String(prvBytes, Base64FormattingOptions.InsertLineBreaks);
        return (
            $"-----BEGIN PUBLIC KEY-----\n{pub64}\n-----END PUBLIC KEY-----",
            $"-----BEGIN PRIVATE KEY-----\n{prv64}\n-----END PRIVATE KEY-----"
        );
    }
}
