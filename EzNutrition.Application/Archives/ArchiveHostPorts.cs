using EzNutrition.Archives.Contracts.Serialization;

namespace EzNutrition.Application.Archives;

/// <summary>
/// 指定宿主管理的档案文档存储能力。
/// </summary>
[Flags]
public enum ArchiveDocumentStoreCapabilities
{
    /// <summary>未提供档案文档存储。</summary>
    None = 0,

    /// <summary>可以新增或覆盖档案文档。</summary>
    Save = 1,

    /// <summary>可以列出和读取档案文档。</summary>
    Browse = 2,

    /// <summary>可以删除一个档案文档。</summary>
    Delete = 4,

    /// <summary>可以清空档案文档存储。</summary>
    Clear = 8
}

/// <summary>
/// 表示宿主保存的编码档案文档元数据。
/// </summary>
public sealed record StoredArchiveDocumentInfo
{
    /// <summary>获取宿主文档标识。</summary>
    public required Guid DocumentId { get; init; }

    /// <summary>
    /// 获取用于归组多次咨询的患者逻辑标识；旧版未建立索引的文档为空。
    /// </summary>
    public Guid? PatientId { get; init; }

    /// <summary>获取档案标题。</summary>
    public required string Title { get; init; }

    /// <summary>获取咨询对象显示文本。</summary>
    public required string SubjectDisplay { get; init; }

    /// <summary>获取咨询开始时间。</summary>
    public required DateTimeOffset ConsultationStartedAt { get; init; }

    /// <summary>获取最近保存时间。</summary>
    public required DateTimeOffset LastSavedAt { get; init; }

    /// <summary>获取格式稳定标识。</summary>
    public required string FormatIdentifier { get; init; }

    /// <summary>获取精确格式版本。</summary>
    public required string FormatVersion { get; init; }

    /// <summary>获取媒体类型。</summary>
    public required string MediaType { get; init; }

    /// <summary>获取格式实现声明的可选人类可读名称。</summary>
    public string? FormatDisplayName { get; init; }

    /// <summary>获取格式实现声明的可选首选文件扩展名。</summary>
    public string? PreferredFileExtension { get; init; }
}

/// <summary>
/// 表示宿主管理的编码档案文档。
/// </summary>
public sealed record StoredArchiveDocument
{
    /// <summary>获取文档元数据。</summary>
    public required StoredArchiveDocumentInfo Info { get; init; }

    /// <summary>获取编码后的完整文档内容。</summary>
    public required ReadOnlyMemory<byte> Content { get; init; }
}

/// <summary>
/// 定义由浏览器、桌面应用或机构适配器提供的本机档案文档存储。
/// </summary>
public interface IArchiveDocumentStore
{
    /// <summary>获取存储能力。</summary>
    ArchiveDocumentStoreCapabilities Capabilities { get; }

    /// <summary>新增或覆盖一个编码档案文档。</summary>
    ValueTask SaveAsync(StoredArchiveDocument document, CancellationToken cancellationToken = default);

    /// <summary>列出存储中的档案文档。</summary>
    ValueTask<IReadOnlyList<StoredArchiveDocumentInfo>> ListAsync(
        CancellationToken cancellationToken = default);

    /// <summary>读取一个编码档案文档；不存在时返回空。</summary>
    ValueTask<StoredArchiveDocument?> GetAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除一个编码档案文档；文档不存在时也视为完成。策略拒绝时抛出
    /// <see cref="UnauthorizedAccessException"/>。
    /// </summary>
    ValueTask DeleteAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空存储中的全部编码档案文档。策略拒绝时抛出
    /// <see cref="UnauthorizedAccessException"/>。
    /// </summary>
    ValueTask ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 表示从宿主外部文档入口取得的内容。
/// </summary>
public sealed record ExternalArchiveDocument
{
    /// <summary>获取可选原始文件名。</summary>
    public string? FileName { get; init; }

    /// <summary>获取可选媒体类型。</summary>
    public string? MediaType { get; init; }

    /// <summary>获取完整文档内容。</summary>
    public required ReadOnlyMemory<byte> Content { get; init; }
}

/// <summary>
/// 表示准备交给宿主保存的外部档案文档。
/// </summary>
public sealed record ArchiveDocumentExport
{
    /// <summary>
    /// 获取不包含患者直接身份信息且不带格式扩展名的建议文件名主体。
    /// </summary>
    public required string SuggestedFileNameStem { get; init; }

    /// <summary>
    /// 获取待导出内容的格式描述；宿主使用其中的媒体类型和首选扩展名完成文件交互。
    /// </summary>
    public required ArchiveFormatDescriptor Format { get; init; }

    /// <summary>获取完整文档内容。</summary>
    public required ReadOnlyMemory<byte> Content { get; init; }
}

/// <summary>
/// 定义由浏览器或桌面宿主提供的外部档案文档交互。
/// </summary>
public interface IArchiveDocumentTransport
{
    /// <summary>获取宿主是否支持打开外部文档。</summary>
    bool CanOpen { get; }

    /// <summary>获取宿主能力和当前策略是否允许保存外部文档。</summary>
    bool CanSave { get; }

    /// <summary>请求用户选择一个外部档案文档；取消时返回空。</summary>
    ValueTask<ExternalArchiveDocument?> OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>请求宿主保存一个外部档案文档。</summary>
    ValueTask SaveAsync(ArchiveDocumentExport document, CancellationToken cancellationToken = default);
}
