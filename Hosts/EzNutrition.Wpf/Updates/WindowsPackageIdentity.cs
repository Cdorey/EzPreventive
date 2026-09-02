using System.Runtime.InteropServices;

namespace EzNutrition.Wpf.Updates;

/// <summary>
/// 判断当前 Windows 进程是否由 MSIX 等应用包赋予包身份。
/// </summary>
internal static class WindowsPackageIdentity
{
    private const int AppModelErrorNoPackage = 15700;

    /// <summary>
    /// 获取当前进程是否具有 Windows 包身份；无法明确判断时保守地避让宿主自更新。
    /// </summary>
    internal static bool IsPackaged
    {
        get
        {
            uint packageFullNameLength = 0;
            var result = GetCurrentPackageFullName(
                ref packageFullNameLength,
                nint.Zero);
            return result != AppModelErrorNoPackage;
        }
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern int GetCurrentPackageFullName(
        ref uint packageFullNameLength,
        nint packageFullName);
}
