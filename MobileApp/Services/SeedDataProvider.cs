using Library.Entities;
using MobileApp.Models;

namespace MobileApp.Services;

public static class SeedDataProvider
{
    public static List<AppointmentSummary> GetAllAppointments()
    {
        var today = DateTime.Today;

        return
        [
            // Sarah Mitchell's appointments
            new()
            {
                Id = "seed-1", CustomerEmail = "alex.chen@agendabuddy.dev",
                CustomerName = "Alex Chen", CustomerPhone = "+1 (415) 555-0142",
                ProviderEmail = "sarah.mitchell@agendabuddy.dev", ProviderName = "Sarah Mitchell",
                ScheduledAt = today.AddHours(9), Status = AppointmentStatus.Confirmed,
                ServiceName = "Personal Training", CustomerNotes = "Focus on upper body today, shoulder has been tight"
            },
            new()
            {
                Id = "seed-2", CustomerEmail = "priya.sharma@agendabuddy.dev",
                CustomerName = "Priya Sharma", CustomerPhone = "+1 (628) 555-0198",
                ProviderEmail = "sarah.mitchell@agendabuddy.dev", ProviderName = "Sarah Mitchell",
                ScheduledAt = today.AddHours(10), Status = AppointmentStatus.Confirmed,
                ServiceName = "Yoga Session", CustomerNotes = "Beginner level, working on flexibility"
            },
            new()
            {
                Id = "seed-3", CustomerEmail = "david.thompson@agendabuddy.dev",
                CustomerName = "David Thompson", CustomerPhone = "+1 (510) 555-0267",
                ProviderEmail = "sarah.mitchell@agendabuddy.dev", ProviderName = "Sarah Mitchell",
                ScheduledAt = today.AddHours(14), Status = AppointmentStatus.Requested,
                ServiceName = "HIIT Coaching", CustomerNotes = "First session — wants to discuss goals"
            },
            new()
            {
                Id = "seed-4", CustomerEmail = "priya.sharma@agendabuddy.dev",
                CustomerName = "Priya Sharma", CustomerPhone = "+1 (628) 555-0198",
                ProviderEmail = "sarah.mitchell@agendabuddy.dev", ProviderName = "Sarah Mitchell",
                ScheduledAt = today.AddHours(15).AddMinutes(30), Status = AppointmentStatus.Confirmed,
                ServiceName = "Meditation", CustomerNotes = ""
            },
            new()
            {
                Id = "seed-5", CustomerEmail = "alex.chen@agendabuddy.dev",
                CustomerName = "Alex Chen", CustomerPhone = "+1 (415) 555-0142",
                ProviderEmail = "sarah.mitchell@agendabuddy.dev", ProviderName = "Sarah Mitchell",
                ScheduledAt = today.AddDays(1).AddHours(9), Status = AppointmentStatus.Confirmed,
                ServiceName = "Personal Training", CustomerNotes = "Leg day, bring knee brace"
            },
            new()
            {
                Id = "seed-6", CustomerEmail = "david.thompson@agendabuddy.dev",
                CustomerName = "David Thompson", CustomerPhone = "+1 (510) 555-0267",
                ProviderEmail = "sarah.mitchell@agendabuddy.dev", ProviderName = "Sarah Mitchell",
                ScheduledAt = today.AddDays(1).AddHours(11), Status = AppointmentStatus.Requested,
                ServiceName = "HIIT Coaching", CustomerNotes = "Can we do outdoor if weather is good?"
            },
            new()
            {
                Id = "seed-7", CustomerEmail = "priya.sharma@agendabuddy.dev",
                CustomerName = "Priya Sharma", CustomerPhone = "+1 (628) 555-0198",
                ProviderEmail = "sarah.mitchell@agendabuddy.dev", ProviderName = "Sarah Mitchell",
                ScheduledAt = today.AddDays(2).AddHours(10), Status = AppointmentStatus.Confirmed,
                ServiceName = "Yoga Session", CustomerNotes = "Wants to try hot yoga format"
            },

            // Maria Gonzalez's appointments
            new()
            {
                Id = "seed-8", CustomerEmail = "alex.chen@agendabuddy.dev",
                CustomerName = "Alex Chen", CustomerPhone = "+1 (415) 555-0142",
                ProviderEmail = "maria.gonzalez@agendabuddy.dev", ProviderName = "Maria Gonzalez",
                ScheduledAt = today.AddHours(9).AddMinutes(30), Status = AppointmentStatus.Confirmed,
                ServiceName = "Individual Therapy", CustomerNotes = "Anxiety management follow-up"
            },
            new()
            {
                Id = "seed-9", CustomerEmail = "priya.sharma@agendabuddy.dev",
                CustomerName = "Priya Sharma", CustomerPhone = "+1 (628) 555-0198",
                ProviderEmail = "maria.gonzalez@agendabuddy.dev", ProviderName = "Maria Gonzalez",
                ScheduledAt = today.AddHours(11), Status = AppointmentStatus.Confirmed,
                ServiceName = "Couples Counseling", CustomerNotes = "Communication exercises"
            },
            new()
            {
                Id = "seed-10", CustomerEmail = "david.thompson@agendabuddy.dev",
                CustomerName = "David Thompson", CustomerPhone = "+1 (510) 555-0267",
                ProviderEmail = "maria.gonzalez@agendabuddy.dev", ProviderName = "Maria Gonzalez",
                ScheduledAt = today.AddHours(14).AddMinutes(30), Status = AppointmentStatus.Confirmed,
                ServiceName = "Mindfulness Workshop", CustomerNotes = "First time, interested in meditation basics"
            },
            new()
            {
                Id = "seed-11", CustomerEmail = "alex.chen@agendabuddy.dev",
                CustomerName = "Alex Chen", CustomerPhone = "+1 (415) 555-0142",
                ProviderEmail = "maria.gonzalez@agendabuddy.dev", ProviderName = "Maria Gonzalez",
                ScheduledAt = today.AddDays(1).AddHours(10), Status = AppointmentStatus.Confirmed,
                ServiceName = "Individual Therapy", CustomerNotes = "Progress review session"
            },
            new()
            {
                Id = "seed-12", CustomerEmail = "priya.sharma@agendabuddy.dev",
                CustomerName = "Priya Sharma", CustomerPhone = "+1 (628) 555-0198",
                ProviderEmail = "maria.gonzalez@agendabuddy.dev", ProviderName = "Maria Gonzalez",
                ScheduledAt = today.AddDays(2).AddHours(13), Status = AppointmentStatus.Requested,
                ServiceName = "Couples Counseling", CustomerNotes = "Rescheduled from last week"
            },

            // James Okafor's appointments
            new()
            {
                Id = "seed-13", CustomerEmail = "alex.chen@agendabuddy.dev",
                CustomerName = "Alex Chen", CustomerPhone = "+1 (415) 555-0142",
                ProviderEmail = "james.okafor@agendabuddy.dev", ProviderName = "James Okafor",
                ScheduledAt = today.AddHours(10).AddMinutes(30), Status = AppointmentStatus.Confirmed,
                ServiceName = "Python Tutoring", CustomerNotes = "Working on async/await concepts"
            },
            new()
            {
                Id = "seed-14", CustomerEmail = "david.thompson@agendabuddy.dev",
                CustomerName = "David Thompson", CustomerPhone = "+1 (510) 555-0267",
                ProviderEmail = "james.okafor@agendabuddy.dev", ProviderName = "James Okafor",
                ScheduledAt = today.AddHours(13), Status = AppointmentStatus.Confirmed,
                ServiceName = "Full-Stack Bootcamp Prep", CustomerNotes = "Mock interview practice"
            },
            new()
            {
                Id = "seed-15", CustomerEmail = "priya.sharma@agendabuddy.dev",
                CustomerName = "Priya Sharma", CustomerPhone = "+1 (628) 555-0198",
                ProviderEmail = "james.okafor@agendabuddy.dev", ProviderName = "James Okafor",
                ScheduledAt = today.AddDays(1).AddHours(14), Status = AppointmentStatus.Confirmed,
                ServiceName = "Weekly Code Review", CustomerNotes = "PR #42 needs discussion"
            }
        ];
    }

    public static List<AppointmentSummary> GetForUser(string email, bool isProvider, bool isCustomer)
    {
        var all = GetAllAppointments();

        List<AppointmentSummary> filtered;
        if (isProvider)
            filtered = all.Where(a => a.ProviderEmail.Equals(email, StringComparison.OrdinalIgnoreCase)).ToList();
        else if (isCustomer)
            filtered = all.Where(a => a.CustomerEmail.Equals(email, StringComparison.OrdinalIgnoreCase)).ToList();
        else
            filtered = all;

        foreach (var a in filtered)
            a.DisplayName = isCustomer ? a.ProviderName : a.CustomerName;

        return filtered;
    }

    public static List<CalendarDaySummary> GetCalendarWeek(string email, bool isProvider, bool isCustomer)
    {
        var all = GetAllAppointments();
        var today = DateTime.Today;
        var days = new List<CalendarDaySummary>();

        for (var i = 0; i < 7; i++)
        {
            var date = today.AddDays(i);

            var dayAppointments = all
                .Where(a => a.ScheduledAt.Date == date)
                .Where(a => isProvider
                    ? a.ProviderEmail.Equals(email, StringComparison.OrdinalIgnoreCase)
                    : isCustomer
                        ? a.CustomerEmail.Equals(email, StringComparison.OrdinalIgnoreCase)
                        : true)
                .ToList();

            var booked = dayAppointments
                .Select(a => $"{a.ScheduledAt:h:mm tt} — {(isCustomer ? a.ProviderName : a.CustomerName)}")
                .ToList();

            var available = new List<string>();
            if (isProvider)
            {
                var bookedHours = dayAppointments.Select(a => a.ScheduledAt.Hour).ToHashSet();
                var slots = new[] { 8, 9, 10, 11, 13, 14, 15, 16 };
                available = slots
                    .Where(h => !bookedHours.Contains(h))
                    .Select(h => DateTime.Today.AddHours(h).ToString("h:mm tt"))
                    .ToList();
            }

            days.Add(new CalendarDaySummary
            {
                Date = date.ToString("yyyy-MM-dd"),
                BookedSlots = booked,
                AvailableSlots = available
            });
        }

        return days;
    }
}
