using EzNutrition.Archives.Contracts.Validation;

namespace EzNutrition.Archives.Contracts.Serialization;

/// <summary>
/// 表示档案读取操作的结构化结果。
/// </summary>
public sealed record ArchiveReadResult
{
    /// <summary>
    /// 获取成功读取并迁移到当前语义模型的档案及回写上下文。
    /// </summary>
    public ArchiveDocument? Document { get; init; }

    /// <summary>
    /// 获取读取、兼容和语义校验结果。
    /// </summary>
    public required ArchiveValidationResult Validation { get; init; }

    /// <summary>
    /// 获取读取结果中是否包含当前实现无法解释但已按策略保留的内容。
    /// </summary>
    public bool ContainsUnknownContent => Document?.ContainsUnknownContent == true;

    /// <summary>
    /// 获取是否成功产生可用且无阻断错误的 Bundle。
    /// </summary>
    public bool IsSuccess => Document is not null && !Validation.HasErrors;
}

/// <summary>
/// 表示档案写出操作的结构化结果。
/// </summary>
public sealed record ArchiveWriteResult
{
    /// <summary>
    /// 获取目标格式和版本。
    /// </summary>
    public required ArchiveFormatDescriptor TargetFormat { get; init; }

    /// <summary>
    /// 获取写出前后的结构、兼容和语义校验结果。
    /// </summary>
    public required ArchiveValidationResult Validation { get; init; }

    /// <summary>
    /// 获取是否成功完成写出。
    /// </summary>
    public bool IsSuccess => !Validation.HasErrors;
}
