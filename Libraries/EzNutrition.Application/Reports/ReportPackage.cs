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

    /// <summary>保留读取时的容器版本，用于原包导入的索引；新生成报告采用当前版本。</summary>
    internal string? SourcePackageVersion { get; init; }

    /// <summary>获取各历史版本的 PDF 原件，以确切版本标识定位。</summary>
    public IReadOnlyDictionary<Guid, ReadOnlyMemory<byte>> PreviousPdfs { get; init; } =
        new Dictionary<Guid, ReadOnlyMemory<byte>>();

    /// <summary>获取已经验证为单链的报告版本，按修订号排列；顺序本身不替代 Supersedes 校验。</summary>
    public IReadOnlyList<NutritionReportResource> Versions => Document.Bundle.Entries.OfType<NutritionReportResource>()
        .OrderBy(report => report.Metadata.RevisionNumber.Value).ToArray();

    /// <summary>获取当前正式版本。报告包读取和写出前须核对完整版本链。</summary>
    public NutritionReportResource Report => Versions[^1];

    /// <summary>获取稳定的宿主文档标识，沿用初版报告的版本标识，修订不另建索引。</summary>
    public Guid DocumentId => Versions[0].Metadata.VersionId.Value;

    /// <summary>获取当前报告所属的确切咨询快照。</summary>
    public ConsultationResource Consultation => Document.Bundle.Entries.OfType<ConsultationResource>()
        .Single(resource => resource.Metadata.VersionId == Report.ConsultationReference.VersionId);
}

/// <summary>
/// 将现有档案格式和 PDF 原件封装为一个本机报告包；不把附件字节或文件路径加入档案契约。
/// </summary>
/// <remarks>
/// 一个包保存清单、档案、当前 PDF 及各历史 PDF。读取时限制解压大小并直接读取指定条目，
/// 不解压到文件系统；外部包不会控制本机路径。版本 2 承载一条完整报告修订链，兼容读取版本 1。
/// </remarks>
public sealed class ReportPackage(IEnumerable<IArchiveCodec> codecs, IArchiveValidator validator)
{
    /// <summary>获取报告包的稳定格式身份。</summary>
    public static ArchiveFormatDescriptor Format { get; } = new(
        new Uri("https://eznutrition.cdorey.net/formats/report-package"), "2",
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
        if (archiveContent.Length + report.Pdf.Length + report.PreviousPdfs.Values.Sum(pdf => (long)pdf.Length) > MaximumBytes - 4096)
            throw new InvalidDataException("报告包超过允许的大小。");

        var manifest = new Manifest(2, choice.Format.Identifier.AbsoluteUri, choice.Format.Version);
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            await WriteEntry(zip, "manifest.json", JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions), cancellationToken);
            await WriteEntry(zip, "archive", archiveContent.ToArray(), cancellationToken);
            await WriteEntry(zip, "report.pdf", report.Pdf, cancellationToken);
            foreach (var previous in report.PreviousPdfs.OrderBy(pair => pair.Key))
                await WriteEntry(zip, HistoryPath(previous.Key), previous.Value, cancellationToken);
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
        if (zip.Entries.Count is < 3 or > 1024
            || zip.Entries.Select(entry => entry.FullName).Distinct(StringComparer.Ordinal).Count() != zip.Entries.Count
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
        if (manifest.Version is not (1 or 2)) throw new InvalidDataException("当前版本不支持该报告包格式。");
        var codec = codecs.FirstOrDefault(candidate => candidate.ReadableFormats.Any(format =>
            format.Identifier.AbsoluteUri == manifest.ArchiveFormat && format.Version == manifest.ArchiveVersion))
            ?? throw new InvalidDataException("当前宿主不能读取报告包内的档案格式。");
        var archiveBytes = await ReadEntry(zip, "archive", MaximumBytes, cancellationToken);
        using var archiveStream = new MemoryStream(archiveBytes, writable: false);
        var read = await codec.ReadAsync(archiveStream, cancellationToken);
        if (!read.IsSuccess || read.Document is null)
            throw new InvalidDataException("报告包内的档案未通过校验。");
        var versions = ValidateHistory(read.Document);
        if (manifest.Version == 1 && versions.Count != 1)
            throw new InvalidDataException("版本 1 的报告包只能包含一个正式报告版本。");
        var names = new HashSet<string>(["manifest.json", "archive", "report.pdf"], StringComparer.Ordinal);
        var previousPdfs = new Dictionary<Guid, ReadOnlyMemory<byte>>();
        foreach (var previous in versions.Take(versions.Count - 1))
        {
            var id = previous.Metadata.VersionId.Value;
            names.Add(HistoryPath(id));
            previousPdfs.Add(id, await ReadEntry(zip, HistoryPath(id), MaximumBytes, cancellationToken));
        }
        if (!names.SetEquals(zip.Entries.Select(entry => entry.FullName)))
            throw new InvalidDataException("报告包包含缺失、多余或无法对应版本的文件。");
        var result = new SignedReport
        {
            Document = read.Document,
            Pdf = await ReadEntry(zip, "report.pdf", MaximumBytes, cancellationToken),
            PreviousPdfs = previousPdfs,
            SourcePackageVersion = manifest.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        Validate(result);
        return result;
    }

    private void Validate(SignedReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var reports = ValidateHistory(report.Document);
        var expected = reports.Take(reports.Count - 1).Select(version => version.Metadata.VersionId.Value).ToHashSet();
        if (!expected.SetEquals(report.PreviousPdfs.Keys))
            throw new InvalidDataException("报告的历史 PDF 与版本记录不对应。");
        ReportPdf.Verify(report.Pdf.Span, reports[^1].RenderedArtifact!);
        foreach (var previous in reports.Take(reports.Count - 1))
            ReportPdf.Verify(report.PreviousPdfs[previous.Metadata.VersionId.Value].Span, previous.RenderedArtifact!);
    }

    /// <summary>
    /// 核对导入包是否完整保留本机既有历史。使用现有 codec 比较相同历史范围的规范写出结果，
    /// 同时覆盖全部资源字段和 PDF；不只比较版本号或指纹，也不另建语义比较器。
    /// </summary>
    public async ValueTask<bool> PreservesHistoryAsync(SignedReport incoming, SignedReport existing,
        CancellationToken cancellationToken = default)
    {
        if (incoming.Document.ContainsUnknownContent || existing.Document.ContainsUnknownContent) return false;
        var prior = existing.Versions;
        if (!incoming.Versions.Take(prior.Count).Select(version => version.Metadata.VersionId)
                .SequenceEqual(prior.Select(version => version.Metadata.VersionId))) return false;
        var resources = incoming.Document.Bundle.Entries.ToDictionary(resource => resource.Metadata.VersionId);
        if (existing.Document.Bundle.Entries.Any(resource => !resources.ContainsKey(resource.Metadata.VersionId))) return false;
        var historical = existing with
        {
            Document = existing.Document with
            {
                Bundle = existing.Document.Bundle with
                {
                    Entries = existing.Document.Bundle.Entries.Select(resource => resources[resource.Metadata.VersionId]).ToArray()
                }
            },
            Pdf = incoming.Report.Metadata.VersionId == existing.Report.Metadata.VersionId
                ? incoming.Pdf : incoming.PreviousPdfs[existing.Report.Metadata.VersionId.Value],
            PreviousPdfs = prior.Take(prior.Count - 1).ToDictionary(version => version.Metadata.VersionId.Value,
                version => incoming.PreviousPdfs[version.Metadata.VersionId.Value])
        };
        var originalBytes = await WriteAsync(existing, cancellationToken);
        var importedBytes = await WriteAsync(historical, cancellationToken);
        return originalBytes.AsSpan().SequenceEqual(importedBytes);
    }

    /// <summary>复用契约校验，并要求此交换包只表达同一报告的完整单链，不按时间戳选取冲突分支。</summary>
    private IReadOnlyList<NutritionReportResource> ValidateHistory(ArchiveDocument document)
    {
        var reports = document.Bundle.Entries.OfType<NutritionReportResource>()
            .OrderBy(report => report.Metadata.RevisionNumber.Value).ToArray();
        if (reports.Length is 0 or > 1022 || reports.Any(report => report.RenderedArtifact is null)
            || validator.ValidateBundle(document.Bundle, ArchiveValidationScope.Finalization).HasErrors)
            throw new InvalidDataException("报告包需要完整有效的签发记录与输入快照。");
        for (var index = 0; index < reports.Length; index++)
        {
            var current = reports[index];
            var metadata = current.Metadata;
            var predecessor = index == 0 ? null : reports[index - 1];
            if (metadata.ResourceId != reports[0].Metadata.ResourceId
                || metadata.RevisionNumber.Value != index + 1
                || current.SubjectReference != reports[0].SubjectReference
                || current.ConsultationReference.ResourceId != reports[0].ConsultationReference.ResourceId
                || (predecessor is null
                    ? metadata.Status != ResourceLifecycleStatus.Final || metadata.Supersedes is not null
                    : metadata.Status != ResourceLifecycleStatus.Amended
                        || metadata.Supersedes?.VersionId != predecessor.Metadata.VersionId
                        || metadata.CreatedAt < predecessor.Metadata.FinalizedAt))
                throw new InvalidDataException("报告修订链不完整、存在冲突，或更改了报告所属对象。");
        }
        return reports;
    }

    private static string HistoryPath(Guid versionId) => $"history/{versionId:N}.pdf";

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
