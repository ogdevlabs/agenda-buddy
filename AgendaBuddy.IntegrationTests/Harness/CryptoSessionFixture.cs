using System.Security.Cryptography;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// The harness's RSA keypair: generated once per test session, held in memory, never written to disk.
/// </summary>
/// <remarks>
/// <para>
/// <c>AgendaBuddy.Identity.Tests/Helpers/RsaKeyHelper.cs</c> is the existing
/// precedent and this follows it, with one deliberate difference: <b>no private-key PEM string is
/// ever produced.</b> Signing takes the live <see cref="RSA"/> instance instead
/// (<see cref="SigningKey"/>), so the private half never exists in a form that can be logged,
/// serialised, written to a scratch file, or pasted into a fixture. This repository is <b>public</b>,
/// which makes a committed key a permanent artifact — see <c>docs/issues/ISSUE-002</c> for this
/// project's own standing proof that deleting a secret from the working tree does not delete it from
/// history.
/// </para>
/// <para>
/// <b>Lifetime.</b> Shared through <see cref="HarnessCollection"/> as an
/// <c>ICollectionFixture</c>, so xUnit constructs exactly one per test session and disposes it at the
/// end. Consumers: <c>TokenFactory</c> (signs with <see cref="SigningKey"/>) and
/// <c>ServiceHostFixture</c> (exports <see cref="PublicKeyPem"/> as
/// <c>JWT_PUBLIC_KEY</c>).
/// </para>
/// </remarks>
public sealed class CryptoSessionFixture : IDisposable
{
    private const string PublicKeyLabel = "PUBLIC KEY";

    private readonly RSA _rsa = RSA.Create(2048);

    public CryptoSessionFixture() =>
        PublicKeyPem = ToPem(PublicKeyLabel, _rsa.ExportSubjectPublicKeyInfo());

    /// <summary>
    /// The public half, in the PEM form <c>AuthenticationExtensions</c> expects from
    /// <c>JWT_PUBLIC_KEY</c>.
    /// </summary>
    public string PublicKeyPem { get; }

    /// <summary>
    /// The live keypair, for signing test tokens. Owned by this fixture — do not dispose it.
    /// </summary>
    public RSA SigningKey => _rsa;

    public void Dispose() => _rsa.Dispose();

    private static string ToPem(string label, byte[] der) =>
        $"-----BEGIN {label}-----\n" +
        Convert.ToBase64String(der, Base64FormattingOptions.InsertLineBreaks) +
        $"\n-----END {label}-----";
}

/// <summary>
/// The xUnit collection every harness test class joins: one <see cref="CryptoSessionFixture"/> for
/// the whole session, and no parallelism between the classes that use it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why parallelism is disabled.</b> <c>Library.ServerAuth/AuthenticationExtensions.cs:12</c> reads
/// <c>JWT_PUBLIC_KEY</c> from the <b>process</b> environment when a service starts. Two test classes
/// starting services concurrently would race on that single global and fail as intermittent 401s
/// pointing nowhere near the cause.
/// </para>
/// <para>
/// <b>Why this is a new definition rather than a reuse.</b> <c>AgendaBuddy.Identity.Tests</c> already solves
/// exactly this problem with <c>Auth/TestCollectionDefinition.cs</c>, and this follows that pattern —
/// but an xUnit collection definition applies only within its own assembly, so the attribute over
/// there has no effect here. The pattern transfers; the type cannot.
/// </para>
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class HarnessCollection : ICollectionFixture<CryptoSessionFixture>
{
    public const string Name = "agenda-buddy-harness";
}
