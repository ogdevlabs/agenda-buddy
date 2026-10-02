using AgendaBuddy.Library.Calendar;

namespace AgendaBuddy.Library.Services;

/// <inheritdoc cref="ICalendarFeedService"/>
public class CalendarFeedService(
    IRepository<CalendarFeedEntity> feedRepository,
    IRepository<ProviderEntity> providerRepository,
    IRepository<CustomerEntity> customerRepository,
    IProviderService providerService) : ICalendarFeedService
{
    public async Task<EnabledCalendarFeed> EnableAsync(string ownerEmail, string? language)
    {
        // Delete first: a fault between the two leaves the owner with no feed, never with two live URLs.
        await feedRepository.DeleteManyAsync(OwnerFilter(ownerEmail));

        var token = CalendarFeedToken.New();
        var feed = new CalendarFeedEntity(ownerEmail, CalendarFeedToken.Hash(token), CalendarFeedLanguage.Normalize(language));
        await feedRepository.InsertAsync(feed);
        return new EnabledCalendarFeed(token, feed.CreatedAt);
    }

    public Task<CalendarFeedEntity?> GetAsync(string ownerEmail) => feedRepository.FindOneAsync(OwnerFilter(ownerEmail));

    public async Task<bool> DisableAsync(string ownerEmail) =>
        await feedRepository.DeleteManyAsync(OwnerFilter(ownerEmail)) > 0;

    public async Task<string?> RenderAsync(string token, DateTime nowUtc)
    {
        // The shape check runs before any lookup, so a malformed probe never reaches the database.
        if (!CalendarFeedToken.IsWellFormed(token))
            return null;

        var feed = await feedRepository.FindOneAsync(new BsonDocument("token_hash", CalendarFeedToken.Hash(token)));
        if (feed is null)
            return null;

        var owner = feed.OwnerEmail;
        var provider = await providerService.FindProvidersAsync(SupportTools<ProviderEntity>.FilterByEmail(owner));
        var appointments = provider is null
            ? await providerService.FindAppointmentsByCustomerAsync(owner)
            : provider.AppointmentEntities;

        var names = await DisplayNamesAsync(appointments
            .Where(appointment => !appointment.DayOff)
            .Select(appointment => CalendarFeedProjection.CounterpartyEmail(appointment, owner)));

        var events = CalendarFeedProjection.Project(appointments, owner, names, feed.Language, nowUtc);
        return IcsWriter.Write(CalendarFeedProjection.CalendarName, events, nowUtc);
    }

    private async Task<IReadOnlyDictionary<string, string>> DisplayNamesAsync(IEnumerable<string> emails)
    {
        var distinct = emails.Where(email => !string.IsNullOrWhiteSpace(email))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (distinct.Count == 0)
            return names;

        var filter = new BsonDocument("email", new BsonDocument("$in", new BsonArray(distinct)));
        foreach (var customer in await customerRepository.FindAllAsync(filter))
            if (customer.Email is not null)
                names.TryAdd(customer.Email, DisplayName(customer.FirstName, customer.LastName));
        foreach (var provider in await providerRepository.FindAllAsync(filter))
            names.TryAdd(provider.Email, DisplayName(provider.FirstName, provider.LastName));

        return names;
    }

    private static string DisplayName(string? first, string? last) =>
        string.Join(' ', new[] { first, last }.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()));

    private static BsonDocument OwnerFilter(string ownerEmail) => new("owner_email", ownerEmail);
}
