namespace AgendaBuddy.IntegrationTests.Harness;

public class FutureSlotTest
{
    [Theory]
    [InlineData(2026, 9, 11, 2026, 9, 11)]
    [InlineData(2026, 9, 12, 2026, 9, 14)]
    [InlineData(2026, 9, 13, 2026, 9, 14)]
    [InlineData(2026, 9, 14, 2026, 9, 14)]
    public void MoveToDefaultWorkingDay_AdvancesWeekendCandidatesToMonday(
        int year,
        int month,
        int day,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        var candidate = new DateTime(year, month, day, 10, 0, 0, DateTimeKind.Utc);

        var result = FutureSlot.MoveToDefaultWorkingDay(candidate);

        Assert.Equal(new DateTime(expectedYear, expectedMonth, expectedDay, 10, 0, 0, DateTimeKind.Utc), result);
    }
}
