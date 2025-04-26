using Microsoft.Extensions.Internal;

namespace Ltbyun.Microsoft.Extensions.Caching.PostgreSQL.Tests;

public class TestClock : ISystemClock
{
    public TestClock()
    {
        UtcNow = new DateTimeOffset(2013, 1, 1, 1, 0, 0, offset: TimeSpan.Zero);
    }

    public DateTimeOffset UtcNow { get; private set; }

    public TestClock Add(TimeSpan timeSpan)
    {
        UtcNow = UtcNow.Add(timeSpan);

        return this;
    }
}
