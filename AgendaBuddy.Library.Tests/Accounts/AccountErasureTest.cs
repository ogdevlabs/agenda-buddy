using AgendaBuddy.Library.Accounts;
using Xunit;

namespace AgendaBuddy.Library.Tests.Accounts;

/// <summary>
/// The tombstone an erased account's traces are rewritten to.
/// </summary>
public class AccountErasureTest
{
    /// <summary>
    /// ⚠️ <b>The whole security property of the tombstone.</b> A tombstone derived from the address — a hash, a
    /// prefix, anything — stays linkable back to the person by testing guesses, because the space of real email
    /// addresses is small enough to enumerate against a known value. That would make the "erasure" a
    /// re-identification vector rather than a deletion.
    /// </summary>
    [Fact]
    public void TwoDeletionsOfTheSameAddressProduceDifferentTombstones()
    {
        var first = AccountErasure.NewTombstone();
        var second = AccountErasure.NewTombstone();

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// <c>.invalid</c> is reserved by RFC 2606 and can never resolve, so a tombstone that leaks into a mail or
    /// push send cannot reach a real inbox.
    /// </summary>
    [Fact]
    public void ATombstoneSitsInAReservedUnroutableDomain()
    {
        Assert.EndsWith("@deleted.invalid", AccountErasure.NewTombstone(), StringComparison.Ordinal);
        Assert.Equal("deleted.invalid", AccountErasure.TombstoneDomain);
    }

    [Fact]
    public void ATombstoneIsRecognisedAsOne()
    {
        Assert.True(AccountErasure.IsTombstone(AccountErasure.NewTombstone()));
    }

    /// <summary>
    /// Recognition is what keeps a repeat deletion idempotent: a second run must not scrub already-scrubbed rows
    /// under a fresh token, which would split one person's history into two.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ada@example.com")]
    [InlineData("someone@deleted.invalid")]          // right domain, not our prefix
    [InlineData("deleted-user-abc12345@example.com")] // right prefix, real domain
    public void ARealAddressIsNotMistakenForATombstone(string? email)
    {
        Assert.False(AccountErasure.IsTombstone(email));
    }

    /// <summary>
    /// Case-insensitive, because addresses arrive normalised inconsistently across this codebase and a tombstone
    /// that stopped being recognised on casing alone would be scrubbed twice.
    /// </summary>
    [Fact]
    public void RecognitionIgnoresCase()
    {
        Assert.True(AccountErasure.IsTombstone(AccountErasure.NewTombstone().ToUpperInvariant()));
    }

    /// <summary>The name a deleted account reads as, joined the way every contact surface joins the two halves.</summary>
    [Fact]
    public void TheTombstoneNameReadsAsADeletedAccount()
    {
        Assert.Equal(
            "Deleted account",
            $"{AccountErasure.TombstoneFirstName} {AccountErasure.TombstoneLastName}");
    }
}
