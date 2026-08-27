using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Repositories;
using AgendaBuddy.Library.Services;
using JetBrains.Annotations;
using MongoDB.Bson;
using Moq;
using Xunit;

namespace AgendaBuddy.Library.Tests.Services;

[TestSubject(typeof(BookingService))]
public class BookingServiceTest
{
    private readonly Mock<IRepository<AppointmentEntity>> _repoMock;
    private readonly BookingService _svc;

    public BookingServiceTest()
    {
        _repoMock = new Mock<IRepository<AppointmentEntity>>();
        _svc = new BookingService(_repoMock.Object);
    }

    [Fact]
    public async Task FindOverlappingAppointmentsAsync_FiltersByProviderAndTimeRange()
    {
        var start = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc);
        BsonDocument? capturedFilter = null;
        _repoMock.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>()))
            .Callback<BsonDocument>(f => capturedFilter = f)
            .ReturnsAsync(new List<AppointmentEntity>());

        await _svc.FindOverlappingAppointmentsAsync("provider@example.com", start, end);

        Assert.NotNull(capturedFilter);
        Assert.Equal("provider@example.com", capturedFilter!["email_provider"].AsString);
        Assert.Equal(end, capturedFilter["start"]["$lt"].ToUniversalTime());
        Assert.Equal(start, capturedFilter["end"]["$gt"].ToUniversalTime());
    }

    [Fact]
    public async Task FindOverlappingAppointmentsAsync_ReturnsWhatTheRepositoryFinds()
    {
        var existing = new List<AppointmentEntity>
        {
            new()
            {
                EmailProvider = "provider@example.com",
                EmailCustomer = "customer@example.com",
                Start = DateTime.UtcNow,
                End = DateTime.UtcNow.AddHours(1)
            }
        };
        _repoMock.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>())).ReturnsAsync(existing);

        var result = await _svc.FindOverlappingAppointmentsAsync(
            "provider@example.com", DateTime.UtcNow, DateTime.UtcNow.AddHours(1));

        Assert.Same(existing, result);
    }
}
