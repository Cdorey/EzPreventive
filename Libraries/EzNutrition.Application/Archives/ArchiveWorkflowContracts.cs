using EzNutrition.Application.Consultations;

namespace EzNutrition.Application.Archives;

/// <summary>
/// 指定当前宿主提供的档案工作流能力。
/// </summary>
[Flags]
public enum ArchiveWorkflowCapabilities
{
    /// <summary>未提供可选档案能力。</summary>
    None = 0,

    /// <summary>可以把当前咨询保存到宿主管理的本机档案库。</summary>
    Save = 1,

    /// <summary>可以浏览并调阅宿主管理的本机档案。</summary>
    Browse = 2,

    /// <summary>可以从外部文档导入档案。</summary>
    Import = 4,

    /// <summary>可以把当前咨询导出为外部文档。</summary>
    Export = 8,

    /// <summary>可以删除宿主管理的一份本机档案。</summary>
    Delete = 16,

    /// <summary>可以清空宿主管理的本机档案库。</summary>
    Clear = 32,

    /// <summary>可以把宿主管理的一份已保存档案导出为外部文档。</summary>
    ExportStored = 64
}

/// <summary>
/// 指定档案操作的终止状态。
/// </summary>
public enum ArchiveOperationStatus
{
    /// <summary>操作成功。</summary>
    Succeeded = 0,

    /// <summary>用户取消了操作。</summary>
    Cancelled = 1,

    /// <summary>当前宿主没有提供所需能力。</summary>
    Unavailable = 2,

    /// <summary>档案未通过格式或语义校验。</summary>
    Invalid = 3,

    /// <summary>操作因存储或传输故障失败。</summary>
    Failed = 4,

    /// <summary>宿主提供相应能力，但当前策略或用户不允许操作。</summary>
    Denied = 5
}

/// <summary>
/// 表示可安全展示的档案操作提示。
/// </summary>
public sealed record ArchiveNotice
{
    /// <summary>获取稳定问题代码。</summary>
    public required string Code { get; init; }

    /// <summary>获取是否阻断当前操作。</summary>
    public required bool IsBlocking { get; init; }

    /// <summary>获取不包含原始敏感内容的提示。</summary>
    public required string Message { get; init; }
}

/// <summary>
/// 表示一次档案操作的通用结果。
/// </summary>
public sealed record ArchiveOperationResult
{
    /// <summary>获取操作状态。</summary>
    public required ArchiveOperationStatus Status { get; init; }

    /// <summary>获取面向用户的简短说明。</summary>
    public required string Message { get; init; }

    /// <summary>获取结构化提示。</summary>
    public IReadOnlyList<ArchiveNotice> Notices { get; init; } = Array.Empty<ArchiveNotice>();

    /// <summary>获取操作是否成功。</summary>
    public bool IsSuccess => Status == ArchiveOperationStatus.Succeeded;
}

/// <summary>
/// 表示档案调阅列表中的一个稳定摘要。
/// </summary>
public sealed record ArchiveRecordSummary
{
    /// <summary>获取宿主管理的文档标识。</summary>
    public required Guid DocumentId { get; init; }

    /// <summary>获取患者逻辑标识；旧版未建立索引的文档为空。</summary>
    public Guid? PatientId { get; init; }

    /// <summary>获取档案显示标题。</summary>
    public required string Title { get; init; }

    /// <summary>获取咨询对象显示文本。</summary>
    public required string SubjectDisplay { get; init; }

    /// <summary>获取咨询开始时间。</summary>
    public required DateTimeOffset ConsultationStartedAt { get; init; }

    /// <summary>获取档案最近保存时间。</summary>
    public required DateTimeOffset LastSavedAt { get; init; }
}

/// <summary>
/// 表示档案只读调阅中的一个文本或绝对时间字段。
/// </summary>
public sealed record ArchiveReviewField
{
    /// <summary>创建文本字段。</summary>
    public ArchiveReviewField(string label, string value)
    {
        Label = label;
        Value = value;
    }

    /// <summary>创建绝对时间字段。</summary>
    public ArchiveReviewField(string label, DateTimeOffset instant)
    {
        Label = label;
        Instant = instant;
    }

    /// <summary>获取字段标签。</summary>
    public string Label { get; }

    /// <summary>获取文本值；时间字段为空。</summary>
    public string? Value { get; }

    /// <summary>获取绝对时间值；文本字段为空。</summary>
    public DateTimeOffset? Instant { get; }
}

/// <summary>
/// 表示档案只读调阅中一组按共同语义组织的详细字段。
/// </summary>
public sealed record ArchiveReviewDetailGroup
{
    /// <summary>获取详情组标题。</summary>
    public required string Title { get; init; }

    /// <summary>获取可选的详情组说明。</summary>
    public string? Description { get; init; }

    /// <summary>获取详情组中的只读字段。</summary>
    public IReadOnlyList<ArchiveReviewField> Fields { get; init; } = Array.Empty<ArchiveReviewField>();
}

/// <summary>
/// 表示档案只读调阅中的一个语义区段。
/// </summary>
public sealed record ArchiveReviewSection
{
    /// <summary>获取区段标题。</summary>
    public required string Title { get; init; }

    /// <summary>获取可选区段说明。</summary>
    public string? Description { get; init; }

    /// <summary>获取区段字段。</summary>
    public IReadOnlyList<ArchiveReviewField> Fields { get; init; } = Array.Empty<ArchiveReviewField>();

    /// <summary>获取需要由用户主动展开查看的详细内容组。</summary>
    public IReadOnlyList<ArchiveReviewDetailGroup> DetailGroups { get; init; } =
        Array.Empty<ArchiveReviewDetailGroup>();
}

/// <summary>
/// 表示与具体编码格式无关的档案只读调阅模型。
/// </summary>
public sealed record ArchiveReview
{
    /// <summary>获取 Bundle 标识。</summary>
    public required Guid BundleId { get; init; }

    /// <summary>获取档案标题。</summary>
    public required string Title { get; init; }

    /// <summary>获取咨询对象显示文本。</summary>
    public required string SubjectDisplay { get; init; }

    /// <summary>获取档案建立时间。</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>获取来源格式的显示文本。</summary>
    public required string FormatDisplay { get; init; }

    /// <summary>获取是否携带当前实现不能解释但已保留的源内容。</summary>
    public required bool ContainsUnknownContent { get; init; }

    /// <summary>获取可用于开始新一次独立咨询的患者上下文。</summary>
    public ArchivePatientContext? PatientContext { get; init; }

    /// <summary>获取调阅区段。</summary>
    public IReadOnlyList<ArchiveReviewSection> Sections { get; init; } = Array.Empty<ArchiveReviewSection>();
}

/// <summary>
/// 表示档案调阅列表结果。
/// </summary>
public sealed record ArchiveBrowseResult
{
    /// <summary>获取操作结果。</summary>
    public required ArchiveOperationResult Operation { get; init; }

    /// <summary>获取按最近保存时间排序的档案摘要。</summary>
    public IReadOnlyList<ArchiveRecordSummary> Records { get; init; } = Array.Empty<ArchiveRecordSummary>();
}

/// <summary>
/// 表示打开档案的结果。
/// </summary>
public sealed record ArchiveOpenResult
{
    /// <summary>获取操作结果。</summary>
    public required ArchiveOperationResult Operation { get; init; }

    /// <summary>获取成功建立的只读调阅模型。</summary>
    public ArchiveReview? Review { get; init; }
}

/// <summary>
/// 定义供 UI 使用的格式无关档案用例。
/// </summary>
public interface IArchiveWorkflow
{
    /// <summary>获取当前宿主提供的能力。</summary>
    ArchiveWorkflowCapabilities Capabilities { get; }

    /// <summary>保存当前咨询到宿主管理的档案库。</summary>
    ValueTask<ArchiveOperationResult> SaveCurrentAsync(
        ConsultationWorkspace workspace,
        CancellationToken cancellationToken = default);

    /// <summary>浏览宿主管理的档案库。</summary>
    ValueTask<ArchiveBrowseResult> BrowseAsync(CancellationToken cancellationToken = default);

    /// <summary>读取指定患者的一次咨询历史，核对正文身份而非仅信任摘要。</summary>
    ValueTask<ConsultationHistoryReadResult> ReadHistoryAsync(
        Guid patientId, Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>调阅宿主管理的一个档案。</summary>
    ValueTask<ArchiveOpenResult> OpenStoredAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 将宿主管理的一份已保存档案按其原始编码内容写出到外部文档。
    /// </summary>
    ValueTask<ArchiveOperationResult> ExportStoredAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    /// <summary>删除宿主管理的一份本机档案。</summary>
    ValueTask<ArchiveOperationResult> DeleteStoredAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    /// <summary>清空宿主管理的本机档案库。</summary>
    ValueTask<ArchiveOperationResult> ClearStoredAsync(CancellationToken cancellationToken = default);

    /// <summary>从宿主选择的外部文档读取档案。</summary>
    ValueTask<ArchiveOpenResult> ImportAsync(CancellationToken cancellationToken = default);

    /// <summary>将当前咨询写出到宿主选择的外部文档。</summary>
    ValueTask<ArchiveOperationResult> ExportCurrentAsync(
        ConsultationWorkspace workspace,
        CancellationToken cancellationToken = default);
}
