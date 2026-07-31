using System.Collections.Generic;
using System.Threading.Tasks;
using Library.Entities;
using Library.Services;
using Moq;
using Xunit;

namespace Library.Tests.Services;

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
        _repoMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<DeviceTokenEntity>());
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
        _repoMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<DeviceTokenEntity> { existing });
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
        _repoMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<DeviceTokenEntity> { entity });

        var result = await _svc.GetByEmailAsync("found@example.com");

        Assert.NotNull(result);
        Assert.Equal("found@example.com", result!.UserEmail);
        Assert.Equal("tok", result.Token);
    }

    [Fact]
    public async Task GetByEmailAsync_NotFound_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<DeviceTokenEntity>());

        var result = await _svc.GetByEmailAsync("missing@example.com");

        Assert.Null(result);
    }
}
