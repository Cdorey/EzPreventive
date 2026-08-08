using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Internal;

namespace EzNutrition.Archives.Contracts.Validation;

/// <summary>
/// 指定档案校验问题的严重程度。
/// </summary>
public enum ArchiveValidationSeverity
{
    /// <summary>
    /// 仅供说明，不影响保存或确认。
    /// </summary>
    Information = 0,

    /// <summary>
    /// 临床或兼容性提示，通常不阻止医师继续。
    /// </summary>
    Warning = 1,

    /// <summary>
    /// 阻止当前操作的错误。
    /// </summary>
    Error = 2,

    /// <summary>
    /// 无法继续安全处理的严重错误。
    /// </summary>
    Fatal = 3
}

/// <summary>
/// 指定档案校验问题所属类别。
/// </summary>
public enum ArchiveValidationCategory
{
    /// <summary>
    /// 格式、类型或结构问题。
    /// </summary>
    Structure = 0,

    /// <summary>
    /// 恶意输入、资源限制或数据泄露风险。
    /// </summary>
    Security = 1,

    /// <summary>
    /// 身份、引用、生命周期或数学一致性问题。
    /// </summary>
    Integrity = 2,

    /// <summary>
    /// 需要专业人员留意但不应穷举禁止的临床合理性问题。
    /// </summary>
    Clinical = 3,

    /// <summary>
    /// 未知版本、扩展或降级风险等兼容性问题。
    /// </summary>
    Compatibility = 4
}

/// <summary>
/// 指定档案校验发生的业务边界。
/// </summary>
public enum ArchiveValidationScope
{
    /// <summary>
    /// 保存可继续编辑的草稿。
    /// </summary>
    DraftSave = 0,

    /// <summary>
    /// 将资源确认为不可变正式记录。
    /// </summary>
    Finalization = 1,

    /// <summary>
    /// 从外部格式读取档案。
    /// </summary>
    Import = 2,

    /// <summary>
    /// 将档案写出到外部格式。
    /// </summary>
    Export = 3
}

/// <summary>
/// 表示一个不携带敏感原始数据的结构化校验问题。
/// </summary>
public sealed record ArchiveValidationIssue
{
    /// <summary>
    /// 获取稳定错误代码。
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// 获取严重程度。
    /// </summary>
    public required ArchiveValidationSeverity Severity { get; init; }

    /// <summary>
    /// 获取问题类别。
    /// </summary>
    public required ArchiveValidationCategory Category { get; init; }

    /// <summary>
    /// 获取面向专业用户或开发人员的简洁说明。
    /// </summary>
    /// <remarks>
    /// 消息不得包含完整 SOAP、患者身份或整段原始档案。
    /// </remarks>
    public required string Message { get; init; }

    /// <summary>
    /// 获取可选的契约对象逻辑路径。
    /// </summary>
    public ArchiveElementPath? Path { get; init; }

    /// <summary>
    /// 获取发生问题的可选资源确切版本引用。
    /// </summary>
    public VersionedResourceReference? ResourceReference { get; init; }
}

/// <summary>
/// 表示一次档案校验的完整结果。
/// </summary>
public sealed record ArchiveValidationResult
{
    private IReadOnlyList<ArchiveValidationIssue> _issues = Array.Empty<ArchiveValidationIssue>();

    /// <summary>
    /// 获取全部结构化校验问题。
    /// </summary>
    public IReadOnlyList<ArchiveValidationIssue> Issues
    {
        get => _issues;
        init => _issues = ArchiveCollections.Freeze(value);
    }

    /// <summary>
    /// 获取结果是否包含阻止当前操作的错误。
    /// </summary>
    public bool HasErrors => Issues.Any(static issue =>
        issue.Severity is ArchiveValidationSeverity.Error or ArchiveValidationSeverity.Fatal);
}
