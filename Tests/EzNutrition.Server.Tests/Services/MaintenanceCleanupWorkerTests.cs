using EzNutrition.Server.Services.Maintenance;

namespace EzNutrition.Server.Tests.Services;

public sealed class MaintenanceCleanupWorkerTests
{
    private static readonly TimeZoneInfo ChinaTimeZone = TimeZoneInfo.CreateCustomTimeZone(
        "UTC+08-Test",
        TimeSpan.FromHours(8),
        "UTC+08-Test",
        "UTC+08-Test");

    [Fact]
    public void Next_start_uses_today_when_configured_local_time_is_still_ahead()
    {
        var nowUtc = new DateTimeOffset(2026, 9, 3, 19, 29, 0, TimeSpan.Zero);

        var nextUtc = MaintenanceCleanupWorker.CalculateNextStartUtc(
            nowUtc,
            new TimeOnly(3, 30),
            ChinaTimeZone);

        Assert.Equal(new DateTimeOffset(2026, 9, 3, 19, 30, 0, TimeSpan.Zero), nextUtc);
    }

    [Fact]
    public void Next_start_uses_tomorrow_when_configured_local_time_has_arrived()
    {
        var nowUtc = new DateTimeOffset(2026, 9, 3, 19, 30, 0, TimeSpan.Zero);

        var nextUtc = MaintenanceCleanupWorker.CalculateNextStartUtc(
            nowUtc,
            new TimeOnly(3, 30),
            ChinaTimeZone);

        Assert.Equal(new DateTimeOffset(2026, 9, 4, 19, 30, 0, TimeSpan.Zero), nextUtc);
    }
}
