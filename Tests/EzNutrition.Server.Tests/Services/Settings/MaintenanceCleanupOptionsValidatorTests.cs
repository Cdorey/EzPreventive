using EzNutrition.Server.Services.Settings;

namespace EzNutrition.Server.Tests.Services.Settings;

public sealed class MaintenanceCleanupOptionsValidatorTests
{
    [Theory]
    [InlineData(false, null, true)]
    [InlineData(true, null, false)]
    [InlineData(true, 1, true)]
    [InlineData(false, 1, true)]
    [InlineData(true, 30, true)]
    [InlineData(false, 0, false)]
    [InlineData(true, -1, false)]
    [InlineData(false, -1, false)]
    public void Each_group_requires_positive_days_when_enabled_and_rejects_invalid_supplied_days(
        bool enabled, int? days, bool valid)
    {
        var certification = new CertificationRequestCleanupOptions
        {
            AutoRejectEnabled = enabled,
            PendingTimeoutDays = days
        };
        var audit = new LlmAuditCleanupOptions
        {
            Enabled = enabled,
            RetentionDays = days
        };

        Assert.Equal(valid, new CertificationRequestCleanupOptionsValidator().Validate(null, certification).Succeeded);
        Assert.Equal(valid, new LlmAuditCleanupOptionsValidator().Validate(null, audit).Succeeded);
    }
}
