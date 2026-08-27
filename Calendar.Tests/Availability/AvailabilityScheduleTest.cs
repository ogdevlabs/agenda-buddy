using System;
using System.Collections.Generic;
using System.Linq;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Tools;
using Xunit;

namespace Calendar.Tests.Availability;

public class AvailabilityScheduleTest
{
    [Fact]
    public void GetThirtyDaysCalendarAvailability_NoAppointments_ReturnsSlots()
    {
        var provider = new ProviderEntity
        {
            FirstName = "Test",
            LastName = "Provider",
            Email = "provider@example.com",
            AppointmentEntities = []
        };

        var slots = SupportTools<ProviderEntity>.GetThirtyDaysCalendarAvailability(provider);

        Assert.NotNull(slots);
        // 30 days * up to 11 hours per day — must have slots
        Assert.NotEmpty(slots);
    }

    [Fact]
    public void GetThirtyDaysCalendarAvailability_BookedSlot_ExcludedFromResult()
    {
        var tomorrow = DateTime.Today.AddDays(1).AddHours(10);
        var provider = new ProviderEntity
        {
            FirstName = "Test",
            LastName = "Provider",
            Email = "provider@example.com",
            AppointmentEntities =
            [
                new AppointmentEntity
                {
                    EmailProvider = "provider@example.com",
                    EmailCustomer = "customer@example.com",
                    Start = tomorrow,
                    End = tomorrow.AddHours(1),
                    AppointmentStatus = AppointmentStatus.Booked
                }
            ]
        };

        var slots = SupportTools<ProviderEntity>.GetThirtyDaysCalendarAvailability(provider);

        Assert.DoesNotContain(tomorrow, slots);
    }

    [Fact]
    public void GetThirtyDaysCalendarAvailability_AllSlotsAreInFuture()
    {
        var provider = new ProviderEntity
        {
            FirstName = "Test",
            LastName = "Provider",
            Email = "provider@example.com",
            AppointmentEntities = []
        };

        var slots = SupportTools<ProviderEntity>.GetThirtyDaysCalendarAvailability(provider);
        var now = DateTime.Now;

        Assert.All(slots, s => Assert.True(s >= DateTime.Today));
    }

    [Fact]
    public void GetThirtyDaysCalendarAvailability_SlotsWithinThirtyDays()
    {
        var provider = new ProviderEntity
        {
            FirstName = "Test",
            LastName = "Provider",
            Email = "provider@example.com",
            AppointmentEntities = []
        };

        var slots = SupportTools<ProviderEntity>.GetThirtyDaysCalendarAvailability(provider);
        var cutoff = DateTime.Today.AddDays(31);

        Assert.All(slots, s => Assert.True(s <= cutoff));
    }

    [Fact]
    public void GetThirtyDaysCalendarAvailability_SlotHoursAreBusinessHours()
    {
        var provider = new ProviderEntity
        {
            FirstName = "Test",
            LastName = "Provider",
            Email = "provider@example.com",
            AppointmentEntities = []
        };

        var slots = SupportTools<ProviderEntity>.GetThirtyDaysCalendarAvailability(provider);

        Assert.All(slots, s =>
        {
            Assert.True(s.Hour >= 9, $"Slot {s} starts before 9 AM");
            Assert.True(s.Hour <= 19, $"Slot {s} starts after 7 PM");
        });
    }
}
