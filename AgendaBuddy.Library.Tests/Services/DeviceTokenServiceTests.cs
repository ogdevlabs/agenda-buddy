using System.Threading.Tasks;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Services;
using MongoDB.Bson;
using Moq;
using Xunit;

namespace AgendaBuddy.Library.Tests.Services;

public class DeviceTokenServiceTests
{
    private readonly Mock<Library.Repositories.IRepository<DeviceTokenEntity>> _repoMock;
    private readonly DeviceTokenService _svc;

    public DeviceTokenServiceTests()
    {
        _repoMock = new Mock<Library.Repositories.IRepository<DeviceTokenEntity>>();
        _svc = new DeviceTokenService(_repoMock.Object);
    }

    [Fact]
    public async Task UpsertAsync_NewUser_CreatesToken()
    {
        _repoMock.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>()))
            .ReturnsAsync((DeviceTokenEntity?)null);
        _repoMock.Setup(r => r.InsertAsync(It.IsAny<DeviceTokenEntity>()))
            .Returns(Task.CompletedTask);

        await _svc.UpsertAsync("new@example.com", "fcm-token-abc", "android");

        _repoMock.Verify(r => r.InsertAsync(It.Is<DeviceTokenEntity>(
            e => e.UserEmail == "new@example.com"
                 && e.Token == "fcm-token-abc"
                 && e.Platform == "android")), Times.Once);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<string>(), It.IsAny<DeviceTokenEntity>()), Times.Never);
    }

    [Fact]
    public async Task UpsertAsync_ExistingUser_UpdatesToken()
    {
        var existing = new DeviceTokenEntity
        {
            Id = "507f1f77bcf86cd799439011",
            UserEmail = "existing@example.com",
            Token = "old-token",
            Platform = "android"
        };
        _repoMock.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>()))
            .ReturnsAsync(existing);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<string>(), It.IsAny<DeviceTokenEntity>()))
            .ReturnsAsync(true);

        await _svc.UpsertAsync("existing@example.com", "new-token", "ios");

        _repoMock.Verify(r => r.UpdateAsync("507f1f77bcf86cd799439011", It.Is<DeviceTokenEntity>(
            e => e.UserEmail == "existing@example.com"
                 && e.Token == "new-token"
                 && e.Platform == "ios")), Times.Once);
        _repoMock.Verify(r => r.InsertAsync(It.IsAny<DeviceTokenEntity>()), Times.Never);
    }

    // ── One device, one account ─────────────────────────────────────────────────────────────────────
    // A device token identifies a DEVICE, so at most one account may be addressable through it. Without the
    // eviction below, signing in as B on a phone previously signed in as A left A's row pointing at the same
    // token — and every notification for A, subject and body included, was pushed to a device A had given up.

    [Fact]
    public async Task UpsertAsync_TakesTheTokenAwayFromEveryOtherAccountHoldingIt()
    {
        var stale = new DeviceTokenEntity
        {
            Id = "507f1f77bcf86cd799439099",
            UserEmail = "previous@example.com",
            Token = "shared-device-token",
            Platform = "ios"
        };

        _repoMock.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>())).ReturnsAsync((DeviceTokenEntity?)null);
        _repoMock.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>())).ReturnsAsync([stale]);

        await _svc.UpsertAsync("next@example.com", "shared-device-token", "ios");

        _repoMock.Verify(r => r.DeleteAsync("507f1f77bcf86cd799439099"), Times.Once);
    }

    /// <summary>
    /// Scoped to the token, and excluding the account that just claimed it — an eviction that matched this
    /// account too would delete the row it had only just written.
    /// </summary>
    [Fact]
    public async Task UpsertAsync_MatchesTheTokenAndExcludesTheClaimingAccount()
    {
        BsonDocument? filter = null;
        _repoMock.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>())).ReturnsAsync((DeviceTokenEntity?)null);
        _repoMock.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>()))
            .Callback<BsonDocument>(f => filter = f)
            .ReturnsAsync([]);

        await _svc.UpsertAsync("next@example.com", "shared-device-token", "android");

        Assert.Equal("shared-device-token", filter!["token"].AsString);
        Assert.Equal("next@example.com", filter["user_email"]["$ne"].AsString);
    }

    // The eviction runs after this account's own write, so a fault between the two leaves the previous holder
    // addressable rather than leaving nobody addressable.
    [Fact]
    public async Task UpsertAsync_WritesThisAccountsRowBeforeEvictingAnyOther()
    {
        var calls = new List<string>();

        _repoMock.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>())).ReturnsAsync((DeviceTokenEntity?)null);
        _repoMock.Setup(r => r.InsertAsync(It.IsAny<DeviceTokenEntity>()))
            .Callback(() => calls.Add("insert"))
            .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>()))
            .Callback(() => calls.Add("evict"))
            .ReturnsAsync([]);

        await _svc.UpsertAsync("next@example.com", "tok", "android");

        Assert.Equal(["insert", "evict"], calls);
    }

    // Nothing to evict is the ordinary case — a device nobody else has ever signed in on.
    [Fact]
    public async Task UpsertAsync_WithNoOtherAccountHoldingTheToken_DeletesNothing()
    {
        _repoMock.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>())).ReturnsAsync((DeviceTokenEntity?)null);
        _repoMock.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>())).ReturnsAsync([]);

        await _svc.UpsertAsync("only@example.com", "tok", "android");

        _repoMock.Verify(r => r.DeleteAsync(It.IsAny<string>()), Times.Never);
    }

    // ── Sign-out ────────────────────────────────────────────────────────────────────────────────────
    // The eviction above only fires when somebody ELSE signs in on the device, which may be never. Sign-out is
    // the moment the device stops being this account's, so it has to release the registration itself.

    [Fact]
    public async Task DeleteByEmailAsync_RemovesThisAccountsRegistration()
    {
        BsonDocument? filter = null;
        _repoMock.Setup(r => r.FindOneAndDeleteAsync(It.IsAny<BsonDocument>()))
            .Callback<BsonDocument>(f => filter = f)
            .ReturnsAsync(new DeviceTokenEntity { UserEmail = "leaving@example.com", Token = "tok" });

        Assert.True(await _svc.DeleteByEmailAsync("leaving@example.com"));
        Assert.Equal("leaving@example.com", filter!["user_email"].AsString);
    }

    [Fact]
    public async Task DeleteByEmailAsync_WithNoRegistration_ReportsFalseWithoutFailing()
    {
        _repoMock.Setup(r => r.FindOneAndDeleteAsync(It.IsAny<BsonDocument>()))
            .ReturnsAsync((DeviceTokenEntity?)null);

        Assert.False(await _svc.DeleteByEmailAsync("never-registered@example.com"));
    }

    /// <summary>
    /// An empty address must never reach the filter. <c>{ user_email: "" }</c> would match any row written with
    /// no address, and the delete is scoped by that filter alone.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteByEmailAsync_WithNoAddress_TouchesNothing(string email)
    {
        Assert.False(await _svc.DeleteByEmailAsync(email));

        _repoMock.Verify(r => r.FindOneAndDeleteAsync(It.IsAny<BsonDocument>()), Times.Never);
    }

    [Fact]
    public async Task GetByEmailAsync_Found_ReturnsEntity()
    {
        var entity = new DeviceTokenEntity
        {
            Id = "507f1f77bcf86cd799439011",
            UserEmail = "found@example.com",
            Token = "tok",
            Platform = "ios"
        };
        BsonDocument? filter = null;
        _repoMock.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>()))
            .Callback<BsonDocument>(f => filter = f)
            .ReturnsAsync(entity);

        var result = await _svc.GetByEmailAsync("found@example.com");

        Assert.NotNull(result);
        Assert.Equal("found@example.com", result!.UserEmail);
        Assert.Equal("tok", result.Token);

        // Matched in the database, not by reading the whole collection and filtering in memory. Survivable
        // while nothing called this; it is now on the path of every notification that goes out.
        Assert.Equal("found@example.com", filter!["user_email"].AsString);
        _repoMock.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task GetByEmailAsync_NotFound_ReturnsNull()
    {
        _repoMock.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>()))
            .ReturnsAsync((DeviceTokenEntity?)null);

        var result = await _svc.GetByEmailAsync("missing@example.com");

        Assert.Null(result);
    }
}
