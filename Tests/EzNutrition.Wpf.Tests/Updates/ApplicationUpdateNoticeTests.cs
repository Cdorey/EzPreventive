using EzNutrition.Wpf.Updates;

namespace EzNutrition.Wpf.Tests.Updates;

/// <summary>
/// 验证桌面更新提醒遵循产品的四段版本语义。
/// </summary>
public sealed class ApplicationUpdateNoticeTests
{
    [Theory]
    [InlineData("2.1.0.0", "2.1.1.0", false)]
    [InlineData("2.1.1.0", "2.1.1.1", false)]
    [InlineData("2.1.0.0", "2.2.0.0", true)]
    [InlineData("2.1.0.0", "3.0.0.0", true)]
    public void Compatibility_warning_only_tracks_the_first_two_components(
        string currentVersion,
        string targetVersion,
        bool expectedWarning)
    {
        var update = new ApplicationUpdateNotice(
            Version.Parse(currentVersion),
            Version.Parse(targetVersion));

        Assert.Equal(expectedWarning, update.ChangesCompatibilityLine);
    }
}
