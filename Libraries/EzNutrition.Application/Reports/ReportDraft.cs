using System.Security.Cryptography;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Bundles;
using EzNutrition.Archives.Contracts.Metadata;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.Validation;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Application.Reports;

/// <summary>
/// 保存一次报告预览的独立数据快照及拟签发信息；工作区修改不会进入该预览。
/// </summary>
public class ReportDraft
{
    internal ReportDraft(ArchiveDocument document, ActorReference? signer, SignedReport? previous = null)
    {
        Document = document;
        Signer = signer;
        Previous = previous;
    }

    /// <summary>获取报告及其确切输入资源；此时报告仍为草稿。</summary>
    public ArchiveDocument Document { get; }

    /// <summary>获取拟签发人的身份快照；为空时只能生成带水印的评估稿。</summary>
    public ActorReference? Signer { get; }

    /// <summary>获取本次明确更正的旧报告及历史原件；草稿不会使其失效。</summary>
    public SignedReport? Previous { get; }

    /// <summary>获取本次报告的契约记录。</summary>
    public NutritionReportResource Report => Document.Bundle.Entries.OfType<NutritionReportResource>().Single();

    /// <summary>在签发前检查模块输入是否仍与审核快照一致。</summary>
    public virtual void EnsureCurrent() { }

    /// <summary>
    /// 将已审核的最终 PDF 绑定到报告；调用方须在提交前复核当前签发权限和人员身份。
    /// </summary>
    /// <remarks>
    /// 签发时间取预览中已展示的时间，避免确认后偷偷改变成品。此方法不保存文件，
    /// 也不将其他输入资源的草稿状态提升为整次咨询的正式确认。
    /// </remarks>
    public ArchiveDocument BindSignedPdf(ReadOnlySpan<byte> pdf, IArchiveValidator validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        if (Signer is null)
        {
            throw new InvalidOperationException("评估稿不能作为正式报告归档，请重新进入签发流程。");
        }

        var signed = Report with
        {
            Metadata = Report.Metadata with
            {
                Status = Previous is null ? ResourceLifecycleStatus.Final : ResourceLifecycleStatus.Amended,
                Supersedes = Report.Metadata.BasedOn,
                BasedOn = null,
                FinalizedAt = Report.Metadata.CreatedAt,
                FinalizedBy = Signer
            },
            RenderedArtifact = ReportPdf.Identity(pdf)
        };
        var currentEntries = Document.Bundle.Entries.Select(resource =>
            resource is NutritionReportResource ? signed : resource).ToArray();
        var entries = currentEntries;
        if (Previous is not null)
        {
            var previousIds = Previous.Document.Bundle.Entries.Select(resource => resource.Metadata.VersionId).ToHashSet();
            if (currentEntries.Any(resource => resource is not PatientResource && previousIds.Contains(resource.Metadata.VersionId)))
                throw new InvalidOperationException("新快照重复使用了既有临床资源版本，不能提交。");
            // 复诊可能继续引用同一个不可变 Patient 版本；保留已归档资源，其余快照使用独立版本。
            entries = [.. Previous.Document.Bundle.Entries,
                .. currentEntries.Where(resource => !previousIds.Contains(resource.Metadata.VersionId))];
        }
        var document = Document with
        {
            Bundle = Document.Bundle with
            {
                BundleType = Previous is null ? ArchiveBundleType.ConsultationDocument : ArchiveBundleType.TransferPackage,
                Entries = entries
            }
        };
        var validation = validator.ValidateBundle(document.Bundle, ArchiveValidationScope.Finalization);
        if (validation.HasErrors)
        {
            throw new InvalidOperationException("报告未通过签发校验：" + string.Join("；",
                validation.Issues.Where(issue => issue.Severity is
                    ArchiveValidationSeverity.Error or ArchiveValidationSeverity.Fatal).Select(issue => issue.Message)));
        }

        return document;
    }
}

/// <summary>统一计算和核对 PDF 原件指纹；指纹只用于内容完整性，不代表电子签章。</summary>
public static class ReportPdf
{
    /// <summary>获取正式报告成品使用的媒体类型。</summary>
    public const string MediaType = "application/pdf";

    private static readonly Coding Sha256 = new(
        new Uri("https://eznutrition.cdorey.net/codes/content-digest"), "sha-256");

    /// <summary>对实际 PDF 字节计算 SHA-256；拒绝空内容或明显不是 PDF 的内容。</summary>
    public static ReportArtifactIdentity Identity(ReadOnlySpan<byte> pdf)
    {
        if (!pdf.StartsWith("%PDF-"u8))
        {
            throw new InvalidDataException("报告生成器没有返回 PDF 文件。");
        }

        return new ReportArtifactIdentity(MediaType,
            new ContentFingerprint(Sha256, Convert.ToHexString(SHA256.HashData(pdf))));
    }

    /// <summary>核对原件；不支持的算法或内容不符时禁止把文件当作已签发原件。</summary>
    public static void Verify(ReadOnlySpan<byte> pdf, ReportArtifactIdentity expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        var actual = Identity(pdf);
        if (expected.MediaType != MediaType || expected.Fingerprint.Algorithm.System != Sha256.System
            || expected.Fingerprint.Algorithm.Code != Sha256.Code
            || !string.Equals(actual.Fingerprint.Value, expected.Fingerprint.Value, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("报告 PDF 与签发记录不一致，无法打印原件。");
        }
    }
}
