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
