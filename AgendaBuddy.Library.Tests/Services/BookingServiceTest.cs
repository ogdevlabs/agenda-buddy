using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Repositories;
using AgendaBuddy.Library.Services;
using AgendaBuddy.Library.Tools;
using System.Linq;
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

    /// <summary>
    /// A cancelled appointment does not occupy its slot.
    /// </summary>
    /// <remarks>
    /// Load-bearing since cancellation became a soft delete: without this clause, cancelling would free the
    /// slot on the calendar and still refuse every attempt to rebook it, which is worse than either behaviour
    /// on its own.
    /// </remarks>
    [Fact]
    public async Task FindOverlappingAppointmentsAsync_IgnoresCancelledAppointments()
    {
        BsonDocument? capturedFilter = null;
        _repoMock.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>()))
            .Callback<BsonDocument>(f => capturedFilter = f)
            .ReturnsAsync(new List<AppointmentEntity>());

        await _svc.FindOverlappingAppointmentsAsync(
            "provider@example.com", DateTime.UtcNow, DateTime.UtcNow.AddHours(1));

        Assert.Equal(
            (int)AppointmentStatus.Cancelled,
            capturedFilter!["appointment_status"]["$ne"].AsInt32);
    }

    // ── Cancellation ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A soft delete: a targeted <c>$set</c>, never a delete, so the record that the slot was once booked
    /// survives for reporting and for the notification that names it.
    /// </summary>
    [Fact]
    public async Task CancelAppointmentAsync_SetsTheStatusInsteadOfDeletingTheDocument()
    {
        BsonDocument? filter = null;
        BsonDocument? update = null;
        _repoMock.Setup(r => r.FindOneAndUpdateAsync(It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>()))
            .Callback<BsonDocument, BsonDocument>((f, u) => (filter, update) = (f, u))
            .ReturnsAsync(new AppointmentEntity { EmailProvider = "p@e.test", EmailCustomer = "c@e.test" });

        Assert.True(await _svc.CancelAppointmentAsync("abc123"));

        Assert.Equal("abc123", filter!["identifier"].AsString);
        Assert.Equal((int)AppointmentStatus.Cancelled, update!["$set"]["appointment_status"].AsInt32);
        Assert.Equal(
            EnumHelper<AppointmentStatus>.GetEnumDescription(AppointmentStatus.Cancelled),
            update["$set"]["appointment_description"].AsString);

        // Nothing is ever deleted.
        _repoMock.Verify(r => r.DeleteByIdentifierAsync(It.IsAny<string>()), Times.Never);
        _repoMock.Verify(r => r.DeleteAsync(It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// Only Requested and Booked are cancellable, and that rule is in the FILTER rather than in a preceding
    /// read — so the check and the write are one atomic operation. A read-then-write could see Booked, be
    /// overtaken by a completion, and cancel work that had already been delivered.
    /// </summary>
    [Fact]
    public async Task CancelAppointmentAsync_OnlyMatchesACancellableAppointment()
    {
        BsonDocument? filter = null;
        _repoMock.Setup(r => r.FindOneAndUpdateAsync(It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>()))
            .Callback<BsonDocument, BsonDocument>((f, _) => filter = f)
            .ReturnsAsync((AppointmentEntity?)null);

        await _svc.CancelAppointmentAsync("abc123");

        var cancellable = filter!["appointment_status"]["$in"].AsBsonArray
            .Select(value => value.AsInt32)
            .ToList();

        Assert.Equal(2, cancellable.Count);
        Assert.Contains((int)AppointmentStatus.Requested, cancellable);
        Assert.Contains((int)AppointmentStatus.Booked, cancellable);
        Assert.DoesNotContain((int)AppointmentStatus.Completed, cancellable);
        Assert.DoesNotContain((int)AppointmentStatus.Cancelled, cancellable);
    }

    // A filter that matched nothing means the appointment was completed, already cancelled, or absent -- all of
    // which are "not cancelled by this call".
    [Fact]
    public async Task CancelAppointmentAsync_ReportsFailureWhenNothingMatched()
    {
        _repoMock.Setup(r => r.FindOneAndUpdateAsync(It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>()))
            .ReturnsAsync((AppointmentEntity?)null);

        Assert.False(await _svc.CancelAppointmentAsync("abc123"));
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
