using AgendaBuddy.Library.Calendar;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Repositories;
using AgendaBuddy.Library.Services;
using MongoDB.Bson;
using Moq;
using Xunit;

namespace AgendaBuddy.Library.Tests.Services;

public class CalendarFeedServiceTest
{
    private const string Owner = "coach@example.com";
    private readonly Mock<IRepository<CalendarFeedEntity>> _feeds = new();
    private readonly Mock<IRepository<ProviderEntity>> _providers = new();
    private readonly Mock<IRepository<CustomerEntity>> _customers = new();
    private readonly Mock<IProviderService> _providerService = new();
    private readonly CalendarFeedService _service;

    public CalendarFeedServiceTest()
    {
        _providers.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>())).ReturnsAsync([]);
        _customers.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>())).ReturnsAsync([]);
        _service = new CalendarFeedService(_feeds.Object, _providers.Object, _customers.Object, _providerService.Object);
    }

    [Fact]
    public async Task EnableReplacesAnyExistingFeedAndStoresOnlyTheHash()
    {
        CalendarFeedEntity? stored = null;
        var sequence = new List<string>();
        _feeds.Setup(r => r.DeleteManyAsync(It.IsAny<BsonDocument>())).Callback(() => sequence.Add("delete")).ReturnsAsync(1);
        _feeds.Setup(r => r.InsertAsync(It.IsAny<CalendarFeedEntity>()))
            .Callback<CalendarFeedEntity>(feed => { stored = feed; sequence.Add("insert"); })
            .Returns(Task.CompletedTask);

        var enabled = await _service.EnableAsync(Owner, "es-MX");

        Assert.Equal(["delete", "insert"], sequence);
        Assert.True(CalendarFeedToken.IsWellFormed(enabled.Token));
        Assert.NotNull(stored);
        Assert.Equal(CalendarFeedToken.Hash(enabled.Token), stored!.TokenHash);
        Assert.NotEqual(enabled.Token, stored.TokenHash);
        Assert.Equal("es", stored.Language);
        _feeds.Verify(r => r.DeleteManyAsync(It.Is<BsonDocument>(f => f["owner_email"] == Owner)));
    }

    [Fact]
    public async Task DisableReportsWhetherAFeedExisted()
    {
        _feeds.Setup(r => r.DeleteManyAsync(It.IsAny<BsonDocument>())).ReturnsAsync(0);

        Assert.False(await _service.DisableAsync(Owner));
    }

    [Fact]
    public async Task MalformedTokenNeverReachesTheDatabase()
    {
        Assert.Null(await _service.RenderAsync("../../etc/passwd", DateTime.UtcNow));

        _feeds.Verify(r => r.FindOneAsync(It.IsAny<BsonDocument>()), Times.Never);
    }

    [Fact]
    public async Task UnknownTokenRendersNothing()
    {
        _feeds.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>())).ReturnsAsync((CalendarFeedEntity?)null);

        Assert.Null(await _service.RenderAsync(CalendarFeedToken.New(), DateTime.UtcNow));
    }

    [Fact]
    public async Task KnownTokenRendersTheProvidersAppointmentsWithNames()
    {
        var token = CalendarFeedToken.New();
        var now = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        _feeds.Setup(r => r.FindOneAsync(It.Is<BsonDocument>(f => f["token_hash"] == CalendarFeedToken.Hash(token))))
            .ReturnsAsync(new CalendarFeedEntity(Owner, CalendarFeedToken.Hash(token), "en"));
        var provider = new ProviderEntity { FirstName = "Ana", LastName = "Coach", Email = Owner };
        provider.AppointmentEntities.Add(new AppointmentEntity
        {
            Identifier = "appt-1",
            EmailProvider = Owner,
            EmailCustomer = "client@example.com",
            Start = now.AddDays(1),
            End = now.AddDays(1).AddHours(1),
            AppointmentStatus = AppointmentStatus.Booked,
            ServiceName = "Yoga"
        });
        _providerService.Setup(s => s.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(provider);
        _customers.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>()))
            .ReturnsAsync([new CustomerEntity { FirstName = "Luis", LastName = "Client", Email = "client@example.com" }]);

        var ics = await _service.RenderAsync(token, now);

        Assert.NotNull(ics);
        Assert.Contains("SUMMARY:Yoga with Luis Client\r\n", ics);
        Assert.Contains("UID:appt-1@agendame\r\n", ics);
        Assert.DoesNotContain("@example.com", ics);
    }

    [Fact]
    public async Task CustomerFeedIsGatheredFromTheProviderSide()
    {
        var token = CalendarFeedToken.New();
        _feeds.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>()))
            .ReturnsAsync(new CalendarFeedEntity("client@example.com", CalendarFeedToken.Hash(token), "en"));
        _providerService.Setup(s => s.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync((ProviderEntity)null!);
        _providerService.Setup(s => s.FindAppointmentsByCustomerAsync("client@example.com")).ReturnsAsync([]);

        var ics = await _service.RenderAsync(token, DateTime.UtcNow);

        Assert.NotNull(ics);
        _providerService.Verify(s => s.FindAppointmentsByCustomerAsync("client@example.com"));
    }
}
