namespace Library.Tools;

public static class SupportTools<TEntity> where TEntity : class
{
    public static BsonDocument FilterByNameAndLastName(string firstName, string lastName)
    {
        return new BsonDocument("first_name", firstName).Add("last_name", lastName);
    }

    public static BsonDocument FilterByEmail(string email)
    {
        return new BsonDocument("email", email);
    }
    
    public static BsonDocument FilterByEmailProvider(string email)
    {
        return new BsonDocument("email_provider", email);
    }

    public static List<ServiceEntity> GenerateIdForRecord(List<ServiceEntity> dataCollection)
    {
        foreach (var service in dataCollection)
        {
            service.Id = ObjectId.GenerateNewId();
        }

        return dataCollection;
    }
    
    public static List<DateTime> GetThirtyDaysCalendarAvailability(ProviderEntity providerEntity)
    {
        var appointments = providerEntity.AppointmentEntities;
        DateTime today = DateTime.Today;
        DateTime endDate = today.AddDays(30);

        var allTimeSlots = new List<DateTime>();

        for (DateTime date = today; date <= endDate; date = date.AddDays(1))
        {
            for (int hour = 9; hour < 20; hour++)
            {
                allTimeSlots.Add(date.AddHours(hour));
            }
        }

        var bookedTimeSlots = appointments.Select(a => a.Start).ToHashSet();
        var availableTimeSlots = allTimeSlots.Where(slot => !bookedTimeSlots.Contains(slot)).ToList();
        return availableTimeSlots;
    }
}