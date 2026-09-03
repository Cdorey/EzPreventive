using EzNutrition.Server.Services.Settings;

namespace EzNutrition.Server.Tests.Services.Settings;

public sealed class AccountCleanupOptionsValidatorTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    public void Each_enabled_rule_requires_its_own_positive_retention(int? days, bool valid)
    {
        var validator = new AccountCleanupOptionsValidator();
        AccountCleanupOptions[] candidates =
        [
            new() { UnsubmittedCertificationCleanupEnabled = true, CertificationSubmissionGraceDays = days },
            new() { NonFormalAccountCleanupEnabled = true, NonFormalAccountRetentionDays = days },
            new() { InactiveFormalAccountCleanupEnabled = true, FormalAccountInactivityDays = days }
        ];

        Assert.All(candidates, options => Assert.Equal(valid, validator.Validate(null, options).Succeeded));
    }

    [Fact]
    public void Disabled_rules_may_be_unconfigured_but_any_supplied_interval_must_be_positive()
    {
        var validator = new AccountCleanupOptionsValidator();

        Assert.True(validator.Validate(null, new()).Succeeded);
        Assert.True(validator.Validate(null, new() { SweepIntervalHours = 1 }).Succeeded);
        Assert.True(validator.Validate(null, new() { NonFormalAccountRetentionDays = 1 }).Succeeded);
        Assert.True(validator.Validate(null, new() { CertificationSubmissionGraceDays = 0 }).Failed);
        Assert.True(validator.Validate(null, new() { NonFormalAccountRetentionDays = -1 }).Failed);
        Assert.True(validator.Validate(null, new() { FormalAccountInactivityDays = 0 }).Failed);
        Assert.True(validator.Validate(null, new() { SweepIntervalHours = 0 }).Failed);
    }
}
