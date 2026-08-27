using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// Pins <see cref="CryptoSessionFixture"/> and the collection that shares it — F-016 AC-3, the
/// in-memory half.
/// </summary>
[Collection(HarnessCollection.Name)]
public class CryptoSessionFixtureTest
{
    private readonly CryptoSessionFixture _crypto;

    public CryptoSessionFixtureTest(CryptoSessionFixture crypto) => _crypto = crypto;

    [Fact]
    public void PublicKeyPem_ImportsAsA2048BitRsaKey()
    {
        using var imported = RSA.Create();
        imported.ImportFromPem(_crypto.PublicKeyPem);

        Assert.Equal(2048, imported.KeySize);
    }

    [Fact]
    public void SigningKey_AndPublicKeyPem_AreHalvesOfTheSameKeypair()
    {
        // The property that matters to F-016-T05: a token signed with SigningKey must validate
        // against the PEM that F-016-T06 will export as JWT_PUBLIC_KEY. If these ever drift, every
        // downstream auth test fails as a confusing 401 instead of naming the real cause.
        var payload = Encoding.UTF8.GetBytes("F-016 harness signing probe");
        var signature = _crypto.SigningKey.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        using var verifier = RSA.Create();
        verifier.ImportFromPem(_crypto.PublicKeyPem);

        Assert.True(
            verifier.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
            "SigningKey does not match PublicKeyPem — the fixture is handing out mismatched halves.");
    }

    [Fact]
    public void Fixture_NeverMaterialisesPrivateKeyPem()
    {
        // A deliberate divergence from the AgendaBuddy.Identity.Tests precedent, which returns BOTH PEMs
        // (AgendaBuddy.Identity.Tests/Helpers/RsaKeyHelper.cs:18-21). Nothing in this harness needs a private
        // PEM *string*: signing takes the live RSA instance. A string is the thing that gets logged,
        // written to a scratch file, or pasted into a fixture — so it is never created. AC-3.
        var privateSurface = typeof(CryptoSessionFixture)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Select(member => member.Name)
            .Where(name => name.Contains("Private", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(privateSurface);
    }

    [Fact]
    public void HarnessCollection_SharesOneKeypairAndSerialisesTheTestsThatUseIt()
    {
        // The reason this collection exists. Library.ServerAuth/AuthenticationExtensions.cs:12 reads
        // JWT_PUBLIC_KEY from the PROCESS environment at startup, so test classes that start a
        // service race on a single global. AgendaBuddy.Identity.Tests/Auth/TestCollectionDefinition.cs already
        // solves this — but an xUnit collection definition only applies within its own assembly, so
        // that one cannot be reused here and the harness needs its own.
        var definition = typeof(HarnessCollection).GetCustomAttribute<CollectionDefinitionAttribute>();

        Assert.NotNull(definition);
        Assert.True(
            definition!.DisableParallelization,
            "HarnessCollection must disable parallelization: classes in it mutate the process-wide " +
            "JWT_PUBLIC_KEY environment variable that AuthenticationExtensions reads at startup.");

        Assert.Contains(
            typeof(ICollectionFixture<CryptoSessionFixture>),
            typeof(HarnessCollection).GetInterfaces());
    }
}
