using System.ComponentModel;
using System.Diagnostics;

namespace EzNutrition.Wpf.Desktop;

/// <summary>
/// 通过 Windows Shell 打开文件夹或定位已经写出的文件。
/// </summary>
public sealed class DesktopFileLauncher
{
    /// <summary>在文件资源管理器中打开指定文件夹。</summary>
    public void OpenFolder(string folderPath)
    {
        var fullPath = Path.GetFullPath(folderPath);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException("指定文件夹不存在。");
        }

        Start(new ProcessStartInfo
        {
            FileName = fullPath,
            UseShellExecute = true
        });
    }

    /// <summary>在文件资源管理器中选中指定文件。</summary>
    public void RevealFile(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("指定文件不存在。", fullPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add($"/select,{fullPath}");
        Start(startInfo);
    }

    private static void Start(ProcessStartInfo startInfo)
    {
        try
        {
            _ = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Windows Shell 没有接受打开请求。");
        }
        catch (Win32Exception exception)
        {
            throw new IOException("Windows Shell 无法处理文件系统请求。", exception);
        }
    }
}
