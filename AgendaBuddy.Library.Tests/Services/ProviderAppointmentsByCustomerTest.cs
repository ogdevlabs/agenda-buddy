using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Repositories;
using AgendaBuddy.Library.Services;
using MongoDB.Bson;
using Moq;
using Xunit;

namespace AgendaBuddy.Library.Tests.Services;

/// <summary>
/// A customer's own appointments have to be gathered from the provider side, because appointments are
/// embedded in <c>ProviderEntity.AppointmentEntities</c> and <c>CustomerEntity.AppointmentCollection</c>
/// holds only identifier strings. The Mongo filter matches a provider document when ANY embedded
/// appointment matches, so the returned documents also carry that provider's OTHER customers'
/// appointments — filtering them back out is the load-bearing part of this method.
/// </summary>
public class ProviderAppointmentsByCustomerTest
{
    private const string Customer = "me@example.com";
    private const string OtherCustomer = "someone.else@example.com";

    private static AppointmentEntity Appt(string provider, string customer, DateTime start) => new()
    {
        EmailProvider = provider,
        EmailCustomer = customer,
        Start = start,
        End = start.AddHours(1)
    };

    private static ProviderEntity Provider(string email, params AppointmentEntity[] appointments) => new()
    {
        FirstName = "Test",
        LastName = "Provider",
        Email = email,
        AppointmentEntities = appointments.ToList()
    };

    private static ProviderService BuildService(IEnumerable<ProviderEntity> matched)
    {
        var repo = new Mock<IRepository<ProviderEntity>>();
        repo.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>())).ReturnsAsync(matched);
        return new ProviderService(repo.Object);
    }

    [Fact]
    public async Task FindAppointmentsByCustomer_ExcludesOtherCustomersAppointmentsFromTheSameProvider()
    {
        var mine = Appt("coach@example.com", Customer, new DateTime(2026, 9, 1, 15, 0, 0, DateTimeKind.Utc));
        var theirs = Appt("coach@example.com", OtherCustomer, new DateTime(2026, 9, 1, 16, 0, 0, DateTimeKind.Utc));
        var svc = BuildService([Provider("coach@example.com", mine, theirs)]);

        var result = await svc.FindAppointmentsByCustomerAsync(Customer);

        Assert.Equal([mine], result);
        Assert.DoesNotContain(result, a => a.EmailCustomer == OtherCustomer);
    }

    [Fact]
    public async Task FindAppointmentsByCustomer_GathersAcrossMultipleProviders_OrderedByStart()
    {
        var later = Appt("coach@example.com", Customer, new DateTime(2026, 9, 5, 9, 0, 0, DateTimeKind.Utc));
        var earlier = Appt("tutor@example.com", Customer, new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc));
        var svc = BuildService([Provider("coach@example.com", later), Provider("tutor@example.com", earlier)]);

        var result = await svc.FindAppointmentsByCustomerAsync(Customer);

        Assert.Equal([earlier, later], result);
    }

    [Fact]
    public async Task FindAppointmentsByCustomer_MatchesEmailCaseInsensitively()
    {
        var mine = Appt("coach@example.com", "Me@Example.COM", new DateTime(2026, 9, 1, 15, 0, 0, DateTimeKind.Utc));
        var svc = BuildService([Provider("coach@example.com", mine)]);

        var result = await svc.FindAppointmentsByCustomerAsync(Customer);

        Assert.Equal([mine], result);
    }

    [Fact]
    public async Task FindAppointmentsByCustomer_NoMatchingProviders_ReturnsEmptyRatherThanNull()
    {
        var svc = BuildService([]);

        var result = await svc.FindAppointmentsByCustomerAsync(Customer);

        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
