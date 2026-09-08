using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Repositories;
using AgendaBuddy.Library.Services;
using MongoDB.Bson;
using Moq;
using Xunit;

namespace AgendaBuddy.Library.Tests.Services;

/// <summary>
/// Avatar and consent are written with targeted <c>$set</c>s, one field group at a time.
/// </summary>
/// <remarks>
/// They get dedicated writes rather than riding the profile <c>PUT</c>, which replaces the whole document — and
/// for a provider additionally resets every nested <c>ServiceEntity.Id</c> to <c>ObjectId.Empty</c>
/// (agenda-buddy-2wf). Changing an avatar must not be able to damage a service catalogue.
/// </remarks>
public class ProfileSettingsWriteTest
{
    private const string Email = "ada@example.com";

    private readonly Mock<IRepository<CustomerEntity>> _customers = new();
    private readonly Mock<IRepository<ProviderEntity>> _providers = new();

    private CustomerService Customers() => new(_customers.Object);
    private ProviderService Providers() => new(_providers.Object);

    // ── avatar ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SettingACustomerAvatarWritesOnlyTheAvatarField()
    {
        await Customers().SetAvatarAsync(Email, "avatar_07");

        _customers.Verify(r => r.FindOneAndUpdateAsync(
            It.Is<BsonDocument>(f => Matches(f, "email", Email)),
            It.Is<BsonDocument>(u => IsSetOfExactly(u, "avatar_id")
                                     && u["$set"].AsBsonDocument["avatar_id"].AsString == "avatar_07")), Times.Once);
    }

    [Fact]
    public async Task SettingAProviderAvatarWritesOnlyTheAvatarField()
    {
        await Providers().SetAvatarAsync(Email, "avatar_07");

        _providers.Verify(r => r.FindOneAndUpdateAsync(
            It.Is<BsonDocument>(f => Matches(f, "email", Email)),
            It.Is<BsonDocument>(u => IsSetOfExactly(u, "avatar_id"))), Times.Once);
    }

    /// <summary>A whole-document replace here is the defect these methods exist to avoid.</summary>
    [Fact]
    public async Task NeitherAvatarWriteReplacesTheDocument()
    {
        await Customers().SetAvatarAsync(Email, "avatar_07");
        await Providers().SetAvatarAsync(Email, "avatar_07");

        _customers.Verify(r => r.UpdateAsync(It.IsAny<string>(), It.IsAny<CustomerEntity>()), Times.Never);
        _providers.Verify(r => r.UpdateAsync(It.IsAny<string>(), It.IsAny<ProviderEntity>()), Times.Never);
    }

    // ── consent ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AcceptingBothDocumentsStampsBothTimestamps()
    {
        var at = new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);

        await Customers().SetConsentAsync(Email, at, at);

        _customers.Verify(r => r.FindOneAndUpdateAsync(
            It.IsAny<BsonDocument>(),
            It.Is<BsonDocument>(u => IsSetOfExactly(u, "terms_accepted_at", "privacy_accepted_at")
                                     && !u["$set"].AsBsonDocument["terms_accepted_at"].IsBsonNull
                                     && !u["$set"].AsBsonDocument["privacy_accepted_at"].IsBsonNull)), Times.Once);
    }

    /// <summary>
    /// ⚠️ <b>Not accepted is written as an explicit null, not an omitted field.</b> Omitting it would leave a
    /// previous acceptance standing, so unticking a box would read back as still accepted — the one outcome a
    /// consent record must never produce.
    /// </summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task DecliningIsWrittenAsAnExplicitNull(bool acceptTerms, bool acceptPrivacy)
    {
        var at = DateTime.UtcNow;

        await Customers().SetConsentAsync(Email, acceptTerms ? at : null, acceptPrivacy ? at : null);

        _customers.Verify(r => r.FindOneAndUpdateAsync(
            It.IsAny<BsonDocument>(),
            It.Is<BsonDocument>(u =>
                IsSetOfExactly(u, "terms_accepted_at", "privacy_accepted_at")
                && u["$set"].AsBsonDocument["terms_accepted_at"].IsBsonNull != acceptTerms
                && u["$set"].AsBsonDocument["privacy_accepted_at"].IsBsonNull != acceptPrivacy)), Times.Once);
    }

    /// <summary>Both roles write the same two fields, so a consent record does not depend on which role holds it.</summary>
    [Fact]
    public async Task AProviderRecordsConsentInTheSameTwoFields()
    {
        var at = DateTime.UtcNow;

        await Providers().SetConsentAsync(Email, at, at);

        _providers.Verify(r => r.FindOneAndUpdateAsync(
            It.IsAny<BsonDocument>(),
            It.Is<BsonDocument>(u => IsSetOfExactly(u, "terms_accepted_at", "privacy_accepted_at"))), Times.Once);
    }

    /// <summary>Consent must not disturb the name, the phone number, the services or the appointment list.</summary>
    [Fact]
    public async Task NeitherConsentWriteTouchesAnyOtherField()
    {
        var at = DateTime.UtcNow;

        await Customers().SetConsentAsync(Email, at, at);
        await Providers().SetConsentAsync(Email, at, at);

        _customers.Verify(r => r.UpdateAsync(It.IsAny<string>(), It.IsAny<CustomerEntity>()), Times.Never);
        _providers.Verify(r => r.UpdateAsync(It.IsAny<string>(), It.IsAny<ProviderEntity>()), Times.Never);
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Guarded before indexing: Moq applies an <c>It.Is</c> predicate to every recorded invocation, so an indexer
    /// on a document written by a different call throws instead of simply not matching.
    /// </summary>
    private static bool Matches(BsonDocument filter, string field, string value) =>
        filter.Contains(field) && filter[field].IsString && filter[field].AsString == value;

    /// <summary>
    /// Whether <paramref name="update"/> is a <c>$set</c> and its operand is <b>exactly</b> these fields.
    /// </summary>
    /// <remarks>
    /// "Exactly", not "contains": the point of a targeted write is what it does <i>not</i> touch, and a predicate
    /// that only checked the expected field would pass a write that also replaced the whole profile alongside it.
    /// </remarks>
    private static bool IsSetOfExactly(BsonDocument update, params string[] fields) =>
        update.ElementCount == 1
        && update.Contains("$set")
        && update["$set"].IsBsonDocument
        && update["$set"].AsBsonDocument.Names.OrderBy(name => name, StringComparer.Ordinal)
            .SequenceEqual(fields.OrderBy(name => name, StringComparer.Ordinal));
}
