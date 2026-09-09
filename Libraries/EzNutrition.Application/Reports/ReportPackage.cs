using System.IO.Compression;
using System.Text.Json;
using EzNutrition.Archives.Contracts.Metadata;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.Validation;

namespace EzNutrition.Application.Reports;

/// <summary>表示已经核对契约、引用闭包及成品指纹的正式报告。</summary>
public sealed record SignedReport
{
    /// <summary>获取包含签发记录的档案快照。</summary>
    public required ArchiveDocument Document { get; init; }

    /// <summary>获取签发时保存的 PDF 原件。</summary>
    public required ReadOnlyMemory<byte> Pdf { get; init; }

    /// <summary>获取报告契约记录。</summary>
    public NutritionReportResource Report => Document.Bundle.Entries.OfType<NutritionReportResource>().Single();
}

/// <summary>
/// 将现有档案格式和 PDF 原件封装为一个本机报告包；不把附件字节或文件路径加入档案契约。
/// </summary>
/// <remarks>
/// 一个包只有清单、档案和 PDF 三项。读取时限制解压大小并直接读取指定条目，
/// 不解压到文件系统；外部包不会控制本机路径。当前格式仅承载一份正式报告。
/// </remarks>
public sealed class ReportPackage(IEnumerable<IArchiveCodec> codecs, IArchiveValidator validator)
{
    /// <summary>获取报告包的稳定格式身份。</summary>
    public static ArchiveFormatDescriptor Format { get; } = new(
        new Uri("https://eznutrition.cdorey.net/formats/report-package"), "1",
        "application/vnd.eznutrition.report+zip", "EzNutrition 报告包", ".ezreport");

    /// <summary>获取包和单个正文允许的最大字节数，与本机档案大小限制协调。</summary>
    public const int MaximumBytes = 16 * 1024 * 1024;

    private readonly IArchiveCodec[] codecs = codecs.ToArray();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>识别报告包容器；具体内容仍必须经过完整读取验证。</summary>
    public static bool HasZipHeader(ReadOnlySpan<byte> content) => content.StartsWith("PK\u0003\u0004"u8);

    /// <summary>封装签发后的档案及其对应 PDF，不进行重新渲染。</summary>
    public async ValueTask<byte[]> WriteAsync(SignedReport report, CancellationToken cancellationToken = default)
    {
        Validate(report);
        var choice = codecs.SelectMany(codec => codec.WritableFormats.Select(format => (Codec: codec, Format: format)))
            .OrderBy(item => item.Format.Identifier.AbsoluteUri, StringComparer.Ordinal).FirstOrDefault();
        if (choice.Codec is null)
            throw new InvalidOperationException("当前宿主没有配置报告档案编码器。");

        using var archiveContent = new MemoryStream();
        var written = await choice.Codec.WriteAsync(new ArchiveWriteRequest
        {
            Document = report.Document,
            TargetFormat = choice.Format
        }, archiveContent, cancellationToken);
        if (!written.IsSuccess)
            throw new InvalidDataException("报告档案无法编码，未保存任何正式成品。");
        if (archiveContent.Length + report.Pdf.Length > MaximumBytes)
            throw new InvalidDataException("报告包超过允许的大小。");

        var manifest = new Manifest(1, choice.Format.Identifier.AbsoluteUri, choice.Format.Version);
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            await WriteEntry(zip, "manifest.json", JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions), cancellationToken);
            await WriteEntry(zip, "archive", archiveContent.ToArray(), cancellationToken);
            await WriteEntry(zip, "report.pdf", report.Pdf, cancellationToken);
        }
        if (output.Length > MaximumBytes) throw new InvalidDataException("报告包超过允许的大小。");
        return output.ToArray();
    }

    /// <summary>读取报告包并核对 PDF；任一组成部分缺失或不一致时拒绝原件输出。</summary>
    public async ValueTask<SignedReport> ReadAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        if (content.Length > MaximumBytes || !HasZipHeader(content.Span))
            throw new InvalidDataException("不是受支持的报告包，或报告包过大。");
        using var input = new MemoryStream(content.ToArray(), writable: false);
        using var zip = new ZipArchive(input, ZipArchiveMode.Read);
        if (zip.Entries.Count != 3
            || zip.Entries.Select(entry => entry.FullName).Distinct(StringComparer.Ordinal).Count() != 3
            || zip.Entries.Any(entry => entry.FullName is not ("manifest.json" or "archive" or "report.pdf"))
            || zip.Entries.Any(entry => entry.Length > MaximumBytes)
            || zip.Entries.Sum(entry => entry.Length) > MaximumBytes)
            throw new InvalidDataException("报告包的组成或大小不符合约定。");

        var manifestBytes = await ReadEntry(zip, "manifest.json", 4096, cancellationToken);
        Manifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<Manifest>(manifestBytes, JsonOptions)
                ?? throw new InvalidDataException("报告包缺少清单。");
        }
        catch (JsonException exception) { throw new InvalidDataException("报告包清单无法读取。", exception); }
        if (manifest.Version != 1) throw new InvalidDataException("当前版本不支持该报告包格式。");
        var codec = codecs.FirstOrDefault(candidate => candidate.ReadableFormats.Any(format =>
            format.Identifier.AbsoluteUri == manifest.ArchiveFormat && format.Version == manifest.ArchiveVersion))
            ?? throw new InvalidDataException("当前宿主不能读取报告包内的档案格式。");
        var archiveBytes = await ReadEntry(zip, "archive", MaximumBytes, cancellationToken);
        using var archiveStream = new MemoryStream(archiveBytes, writable: false);
        var read = await codec.ReadAsync(archiveStream, cancellationToken);
        if (!read.IsSuccess || read.Document is null)
            throw new InvalidDataException("报告包内的档案未通过校验。");
        var result = new SignedReport
        {
            Document = read.Document,
            Pdf = await ReadEntry(zip, "report.pdf", MaximumBytes, cancellationToken)
        };
        Validate(result);
        return result;
    }

    private void Validate(SignedReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var reports = report.Document.Bundle.Entries.OfType<NutritionReportResource>().ToArray();
        if (reports.Length != 1 || reports[0].Metadata.Status is not (ResourceLifecycleStatus.Final or ResourceLifecycleStatus.Amended)
            || reports[0].RenderedArtifact is null
            || validator.ValidateBundle(report.Document.Bundle, ArchiveValidationScope.Finalization).HasErrors)
            throw new InvalidDataException("报告包需要一份完整有效的正式签发记录。");
        ReportPdf.Verify(report.Pdf.Span, reports[0].RenderedArtifact!);
    }

    private static async Task WriteEntry(ZipArchive zip, string name, ReadOnlyMemory<byte> bytes, CancellationToken token)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        // 容器不引入当前时钟，便于同一次保存失败后重试；临床时间保留在契约中。
        entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await using var stream = entry.Open();
        await stream.WriteAsync(bytes, token);
    }

    private static async Task<byte[]> ReadEntry(ZipArchive zip, string name, int maximum, CancellationToken token)
    {
        var entry = zip.GetEntry(name) ?? throw new InvalidDataException("报告包缺少必要文件。");
        if (entry.Length <= 0 || entry.Length > maximum) throw new InvalidDataException("报告包条目大小无效。");
        await using var source = entry.Open();
        var bytes = new byte[checked((int)entry.Length)];
        await source.ReadExactlyAsync(bytes, token);
        var extra = new byte[1];
        if (await source.ReadAsync(extra, token) != 0) throw new InvalidDataException("报告包条目的实际大小不符。");
        return bytes;
    }

    private sealed record Manifest(int Version, string ArchiveFormat, string ArchiveVersion);
}
