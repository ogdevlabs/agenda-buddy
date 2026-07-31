using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Library.Entities;
using Library.Repositories;
using Library.Services;
using MongoDB.Bson;
using Moq;
using Xunit;

namespace Library.Tests.Services;

public class ReportingServiceTest
{
    private readonly Mock<IRepository<ProviderEntity>> _providerRepoMock;
    private readonly ReportingService _svc;

    public ReportingServiceTest()
    {
        _providerRepoMock = new Mock<IRepository<ProviderEntity>>();
        _svc = new ReportingService(_providerRepoMock.Object);
    }

    private static AppointmentEntity Appt(string id, string provider, string customer,
        AppointmentStatus status = AppointmentStatus.Requested)
        => new AppointmentEntity
        {
            EmailProvider = provider,
            EmailCustomer = customer,
            AppointmentStatus = status
        };

    private static ProviderEntity BuildProvider(
        string email,
        List<AppointmentEntity> appointments,
        List<ServiceEntity> services)
        => new ProviderEntity
        {
            Email = email,
            FirstName = "Test",
            LastName = "Provider",
            AppointmentEntities = appointments,
            ServiceEntities = services
        };

    [Fact]
    public async Task GetProviderReportAsync_ThrowsKeyNotFound_WhenProviderMissing()
    {
        _providerRepoMock.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>()))
            .ReturnsAsync((ProviderEntity?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _svc.GetProviderReportAsync("missing@example.com"));
    }

    [Fact]
    public async Task GetProviderReportAsync_CountsTotalBookings()
    {
        var appointments = new List<AppointmentEntity>
        {
            Appt("id-1", "p@ex.com", "c1@ex.com"),
            Appt("id-2", "p@ex.com", "c2@ex.com"),
        };
        var provider = BuildProvider("p@ex.com", appointments, new List<ServiceEntity>());
        _providerRepoMock.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>()))
            .ReturnsAsync(provider);

        var report = await _svc.GetProviderReportAsync("p@ex.com");
        Assert.Equal(2, report.TotalBookings);
    }

    [Fact]
    public async Task GetProviderReportAsync_CountsCompletedAppointments()
    {
        var appointments = new List<AppointmentEntity>
        {
            Appt("id-1", "p@ex.com", "c@ex.com", AppointmentStatus.Completed),
            Appt("id-2", "p@ex.com", "c@ex.com"),
        };
        var provider = BuildProvider("p@ex.com", appointments, new List<ServiceEntity>());
        _providerRepoMock.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>()))
            .ReturnsAsync(provider);

        var report = await _svc.GetProviderReportAsync("p@ex.com");
        Assert.Equal(1, report.CompletedAppointments);
    }

    [Fact]
    public async Task GetProviderReportAsync_CountsUniqueCustomers()
    {
        var appointments = new List<AppointmentEntity>
        {
            Appt("id-1", "p@ex.com", "c1@ex.com"),
            Appt("id-2", "p@ex.com", "c1@ex.com"),
            Appt("id-3", "p@ex.com", "c2@ex.com"),
        };
        var provider = BuildProvider("p@ex.com", appointments, new List<ServiceEntity>());
        _providerRepoMock.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>()))
            .ReturnsAsync(provider);

        var report = await _svc.GetProviderReportAsync("p@ex.com");
        Assert.Equal(2, report.UniqueCustomers);
    }

    [Fact]
    public async Task GetProviderReportAsync_CalculatesEstimatedRevenue()
    {
        var appointments = new List<AppointmentEntity>
        {
            Appt("id-1", "p@ex.com", "c@ex.com", AppointmentStatus.Completed),
        };
        var services = new List<ServiceEntity>
        {
            new ServiceEntity("Coaching", "1hr", 50m) { IsActive = true },
        };
        var provider = BuildProvider("p@ex.com", appointments, services);
        _providerRepoMock.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>()))
            .ReturnsAsync(provider);

        var report = await _svc.GetProviderReportAsync("p@ex.com");
        Assert.Equal(50m, report.EstimatedRevenue);
    }

    [Fact]
    public async Task GetProviderReportAsync_RetentionRate_IsZero_WithNoAppointments()
    {
        var provider = BuildProvider("p@ex.com", new List<AppointmentEntity>(), new List<ServiceEntity>());
        _providerRepoMock.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>()))
            .ReturnsAsync(provider);

        var report = await _svc.GetProviderReportAsync("p@ex.com");
        Assert.Equal(0.0, report.RetentionRate);
    }

    [Fact]
    public async Task GetProviderReportAsync_RetentionRate_NonZero_WithReturningCustomer()
    {
        var appointments = new List<AppointmentEntity>
        {
            Appt("id-1", "p@ex.com", "c1@ex.com"),
            Appt("id-2", "p@ex.com", "c1@ex.com"),
        };
        var provider = BuildProvider("p@ex.com", appointments, new List<ServiceEntity>());
        _providerRepoMock.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>()))
            .ReturnsAsync(provider);

        var report = await _svc.GetProviderReportAsync("p@ex.com");
        Assert.Equal(100.0, report.RetentionRate);
    }

    [Fact]
    public void ProviderReport_GeneratedAt_IsSet()
    {
        var r = new ProviderReport();
        Assert.True(r.GeneratedAt <= DateTime.UtcNow);
    }
}
