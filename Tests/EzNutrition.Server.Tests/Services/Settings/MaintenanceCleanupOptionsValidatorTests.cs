using EzNutrition.Server.Services.Settings;

namespace EzNutrition.Server.Tests.Services.Settings;

public sealed class MaintenanceCleanupOptionsValidatorTests
{
    [Theory]
    [InlineData(false, null, null, true)]
    [InlineData(true, null, null, false)]
    [InlineData(true, 1, null, true)]
    [InlineData(false, 1, 1, true)]
    [InlineData(true, 30, 24, true)]
    [InlineData(false, 0, null, false)]
    [InlineData(true, -1, null, false)]
    [InlineData(false, -1, null, false)]
    [InlineData(false, null, 0, false)]
    [InlineData(true, 30, -1, false)]
    public void Each_group_requires_positive_days_when_enabled_and_rejects_invalid_supplied_intervals(
        bool enabled, int? days, int? interval, bool valid)
    {
        var certification = new CertificationRequestCleanupOptions
        {
            AutoRejectEnabled = enabled,
            PendingTimeoutDays = days,
            SweepIntervalHours = interval
        };
        var audit = new LlmAuditCleanupOptions
        {
            Enabled = enabled,
            RetentionDays = days,
            SweepIntervalHours = interval
        };

        Assert.Equal(valid, new CertificationRequestCleanupOptionsValidator().Validate(null, certification).Succeeded);
        Assert.Equal(valid, new LlmAuditCleanupOptionsValidator().Validate(null, audit).Succeeded);
    }
}
