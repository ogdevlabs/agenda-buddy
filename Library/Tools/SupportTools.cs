namespace Library.Tools;

public static class SupportTools<TEntity> where TEntity : class
{
    public static BsonDocument FilterByNameAndLastName(string firstName, string lastName)
    {
        return new BsonDocument("first_name", firstName).Add("last_name", lastName);
    }

    public static BsonDocument FilterByIdentifier(string identifier)
    {
        return new BsonDocument("identifier", identifier);
    }

    public static BsonDocument FilterByEmail(string email)
    {
        return new BsonDocument("email", email);
    }

    public static BsonDocument FilterByName(string name)
    {
        return new BsonDocument("name", name);
    }

    public static BsonDocument FilterByEmailProvider(string email)
    {
        return new BsonDocument("email_provider", email);
    }

    public static List<ServiceEntity> GenerateIdForRecord(List<ServiceEntity> dataCollection)
    {
        foreach (var service in dataCollection) service.Id = ObjectId.GenerateNewId();

        return dataCollection;
    }

    public static List<DateTime> GetThirtyDaysCalendarAvailability(ProviderEntity providerEntity)
    {
        var appointments = providerEntity.AppointmentEntities;
        var today = DateTime.Today;
        var endDate = today.AddDays(30);

        var allTimeSlots = new List<DateTime>();

        for (var date = today; date <= endDate; date = date.AddDays(1))
        {
            var aux = 9;
            if (today == date)
            {
                aux = GetTodayAvailableTime();
                if (aux == 0) continue;
                aux = 19 - aux;
            }

            for (var hour = aux; hour <= 19; hour++) allTimeSlots.Add(date.AddHours(hour));
        }

        var bookedTimeSlots = appointments.Select(a => a.Start).ToHashSet();
        var availableTimeSlots = allTimeSlots.Where(slot => !bookedTimeSlots.Contains(slot)).ToList();
        return availableTimeSlots;
    }

    private static int GetTodayAvailableTime()
    {
        var currentTime = DateTime.Now;
        var startOfAvailability = currentTime.Date.AddHours(9); // 9 AM
        var endOfAvailability = currentTime.Date.AddHours(19); // 7 PM

        if (currentTime < startOfAvailability)
            currentTime = startOfAvailability;
        else if (currentTime >= endOfAvailability.AddHours(-4)) return 0;

        var remainingTime = endOfAvailability - currentTime;

        if (remainingTime.TotalHours < 4) return 0;

        return (int)Math.Floor(remainingTime.TotalHours);
    }
}
