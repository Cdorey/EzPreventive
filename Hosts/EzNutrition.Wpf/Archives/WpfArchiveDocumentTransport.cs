using EzNutrition.Application.Archives;
using EzNutrition.Archives.Xml;
using EzNutrition.Wpf.Desktop;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Windows;

namespace EzNutrition.Wpf.Archives;

/// <summary>
/// 使用 Windows 文件对话框完成外部档案导入导出。
/// </summary>
public sealed class WpfArchiveDocumentTransport : IArchiveDocumentTransport
{
    private readonly DesktopFileLauncher fileLauncher;
    private readonly ILogger<WpfArchiveDocumentTransport> logger;

    /// <summary>
    /// 创建 WPF 档案文档交互适配器。
    /// </summary>
    public WpfArchiveDocumentTransport(
        DesktopFileLauncher fileLauncher,
        ILogger<WpfArchiveDocumentTransport> logger)
    {
        this.fileLauncher = fileLauncher ?? throw new ArgumentNullException(nameof(fileLauncher));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public bool CanOpen => true;

    /// <inheritdoc />
    public bool CanSave => true;

    /// <inheritdoc />
    public async ValueTask<ExternalArchiveDocument?> OpenAsync(
        CancellationToken cancellationToken = default)
    {
        var selectedPath = await InvokeOnUiThreadAsync(() =>
        {
            var dialog = new OpenFileDialog
            {
                Title = "打开 EzNutrition 档案",
                Filter = "EzNutrition XML 档案 (*.xml)|*.xml|所有文件 (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false,
                ValidateNames = true
            };
            return dialog.ShowDialog(CurrentOwner()) == true ? dialog.FileName : null;
        }, cancellationToken);

        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return null;
        }

        return new ExternalArchiveDocument
        {
            FileName = Path.GetFileName(selectedPath),
            MediaType = string.Equals(Path.GetExtension(selectedPath), ".xml", StringComparison.OrdinalIgnoreCase)
                ? XmlArchiveFormat.MediaType
                : null,
            Content = await ArchiveFileIO.ReadAllBytesAsync(selectedPath, cancellationToken)
        };
    }

    /// <inheritdoc />
    public async ValueTask<bool> SaveAsync(
        ArchiveDocumentExport document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var extension = NormalizeExtension(document.Format.PreferredFileExtension);
        var fileName = SanitizeFileName(document.SuggestedFileNameStem) + extension;
        var selectedPath = await InvokeOnUiThreadAsync(() =>
        {
            var dialog = new SaveFileDialog
            {
                Title = "导出 EzNutrition 档案",
                FileName = fileName,
                DefaultExt = extension.TrimStart('.'),
                AddExtension = true,
                OverwritePrompt = true,
                ValidateNames = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Filter = CreateFilter(document.Format.DisplayName, extension)
            };
            return dialog.ShowDialog(CurrentOwner()) == true ? dialog.FileName : null;
        }, cancellationToken);

        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return false;
        }

        await ArchiveFileIO.WriteAtomicallyAsync(selectedPath, document.Content, cancellationToken);
        try
        {
            fileLauncher.RevealFile(selectedPath);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            logger.LogWarning(exception, "The archive was exported but could not be revealed in Explorer.");
        }

        return true;
    }

    private static Window? CurrentOwner() => System.Windows.Application.Current?.MainWindow;

    private static async ValueTask<T> InvokeOnUiThreadAsync<T>(
        Func<T> action,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dispatcher = System.Windows.Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("WPF 应用尚未建立 UI 调度器。");
        if (dispatcher.CheckAccess())
        {
            return action();
        }

        var operation = dispatcher.InvokeAsync(action);
        return await operation.Task.WaitAsync(cancellationToken);
    }

    private static string CreateFilter(string? displayName, string extension)
    {
        var safeDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? "EzNutrition 档案"
            : displayName.Replace('|', ' ').Trim();
        return $"{safeDisplayName} (*{extension})|*{extension}|所有文件 (*.*)|*.*";
    }

    private static string NormalizeExtension(string? extension)
    {
        var value = string.IsNullOrWhiteSpace(extension) ? ".xml" : extension.Trim();
        if (value.Length is < 2 or > 16 ||
            value[0] != '.' ||
            value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            value.IndexOfAny(['/', '\\']) >= 0)
        {
            throw new InvalidDataException("档案格式声明了不安全的文件扩展名。");
        }

        return value.ToLowerInvariant();
    }

    private static string SanitizeFileName(string suggestedStem)
    {
        var stem = string.IsNullOrWhiteSpace(suggestedStem)
            ? "eznutrition-archive"
            : suggestedStem.Trim();
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            stem = stem.Replace(invalidCharacter, '-');
        }

        stem = stem.Trim('.', ' ');
        return string.IsNullOrWhiteSpace(stem) ? "eznutrition-archive" : stem;
    }
}
