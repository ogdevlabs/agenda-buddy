using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Repositories;
using AgendaBuddy.Library.Services;
using MongoDB.Bson;
using Moq;
using Xunit;

namespace AgendaBuddy.Library.Tests.Services;

public class NoteServiceTest
{
    private readonly Mock<IRepository<NoteEntity>> _repoMock;
    private readonly NoteService _svc;

    public NoteServiceTest()
    {
        _repoMock = new Mock<IRepository<NoteEntity>>();
        _svc = new NoteService(_repoMock.Object);
    }

    [Fact]
    public async Task CreateAsync_SetsIdAndTimestamps()
    {
        _repoMock.Setup(r => r.InsertAsync(It.IsAny<NoteEntity>()))
            .Returns(Task.CompletedTask);

        var note = new NoteEntity("p@example.com", "appt-001", "Session went well.");
        var result = await _svc.CreateAsync(note);

        Assert.NotEqual(ObjectId.Empty, result.Id);
        Assert.True(result.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task GetByAppointmentAsync_FiltersCorrectly()
    {
        var notes = new List<NoteEntity>
        {
            new("p@example.com", "appt-001", "Note 1"),
            new("p@example.com", "appt-001", "Note 2"),
        };
        _repoMock.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>()))
            .ReturnsAsync(notes);

        var result = await _svc.GetByAppointmentAsync("p@example.com", "appt-001");
        Assert.Equal(2, ((List<NoteEntity>)result).Count);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsUnauthorized_WhenDifferentProvider()
    {
        var stored = new NoteEntity("owner@example.com", "appt-001", "Original");
        stored.Id = ObjectId.GenerateNewId();
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(stored);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _svc.UpdateAsync(stored.Id.ToString(), "other@example.com", "Hacked"));
    }

    [Fact]
    public async Task UpdateAsync_Succeeds_WhenOwnerMatches()
    {
        var stored = new NoteEntity("p@example.com", "appt-001", "Original");
        stored.Id = ObjectId.GenerateNewId();
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(stored);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<string>(), It.IsAny<NoteEntity>()))
            .ReturnsAsync(true);

        var result = await _svc.UpdateAsync(stored.Id.ToString(), "p@example.com", "Updated content");
        Assert.Equal("Updated content", result.Content);
    }

    [Fact]
    public async Task DeleteAsync_ThrowsUnauthorized_WhenDifferentProvider()
    {
        var stored = new NoteEntity("owner@example.com", "appt-001", "Note");
        stored.Id = ObjectId.GenerateNewId();
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(stored);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _svc.DeleteAsync(stored.Id.ToString(), "attacker@example.com"));
    }

    [Fact]
    public void NoteEntity_DefaultValues()
    {
        var n = new NoteEntity();
        Assert.Null(n.ProviderEmail);
        Assert.Null(n.AppointmentIdentifier);
    }

    [Fact]
    public void NoteEntity_Constructor_SetsFields()
    {
        var n = new NoteEntity("p@example.com", "appt-123", "Good session");
        Assert.Equal("p@example.com", n.ProviderEmail);
        Assert.Equal("appt-123", n.AppointmentIdentifier);
        Assert.Equal("Good session", n.Content);
    }
}
