using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>
    /// F-014 requirement 18 / AC-18. <b>Replaces</b> `GetProviderReportAsync_CalculatesEstimatedRevenue`,
    /// which asserted that one completed appointment against a single 50 service produced revenue of 50.
    /// </summary>
    /// <remarks>
    /// <para>
    /// That test passed, and the behaviour it pinned was wrong. The formula was
    /// <c>completed.Count × sum(all active service fees)</c> — appointments multiplied by the *whole
    /// catalogue* — which happens to be correct only in the single-service case the test used. With three
    /// services at 50, 80 and 100 and two completed appointments it reported 460.
    /// </para>
    /// <para>
    /// And it could not be fixed by changing the formula: <c>AppointmentEntity</c> records no service, no fee
    /// and no amount, so the input does not exist. The field is gone and the report says why — a plausible
    /// number would be believed, which is worse than an honest absence.
    /// </para>
    /// <para>
    /// F-014's one deleted pre-existing test, the same class of deviation as F-016's ADR-025 and F-021's
    /// ADR-034, and it needs the same acknowledgement.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task GetProviderReportAsync_PublishesNoRevenueFigure_AndSaysWhy()
    {
        var appointments = new List<AppointmentEntity>
        {
            Appt("id-1", "p@ex.com", "c@ex.com", AppointmentStatus.Completed),
        };
        var services = new List<ServiceEntity>
        {
            new ServiceEntity("Coaching", "1hr", 50m) { IsActive = true },
            new ServiceEntity("Assessment", "30m", 80m) { IsActive = true },
        };
        var provider = BuildProvider("p@ex.com", appointments, services);
        _providerRepoMock.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>()))
            .ReturnsAsync(provider);

        var report = await _svc.GetProviderReportAsync("p@ex.com");

        Assert.False(report.RevenueAvailable);
        Assert.Equal(ReportingService.RevenueUnavailable, report.RevenueUnavailableReason);

        // The counts still work, and they are what makes the report worth publishing at all.
        Assert.Equal(1, report.CompletedAppointments);
        Assert.Equal(1, report.TotalBookings);

        // No property named anything like a revenue figure survives, so a client cannot bind to one and get a
        // default. Asserted by reflection because the point is the ABSENCE of a field.
        Assert.DoesNotContain(
            typeof(ProviderReport).GetProperties().Select(p => p.Name),
            name => name.Contains("Revenue", StringComparison.OrdinalIgnoreCase)
                    && name is not nameof(ProviderReport.RevenueAvailable)
                    and not nameof(ProviderReport.RevenueUnavailableReason));
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
