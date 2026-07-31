using Library.Tools;

namespace Identity.Tests.Helpers;

public class FakeDateTimeProvider(DateTime utcNow) : IDateTimeProvider
{
    public DateTime UtcNow => utcNow;
}
