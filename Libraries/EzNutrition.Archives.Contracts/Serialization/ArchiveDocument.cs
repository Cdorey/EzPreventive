using EzNutrition.Archives.Contracts.Bundles;

namespace EzNutrition.Archives.Contracts.Serialization;

/// <summary>
/// 表示由编解码器保管的格式专用回写状态。
/// </summary>
/// <remarks>
/// 状态可以保存语义模型尚未解释的源内容；调用方应将其与档案内容按相同敏感级别处理。
/// </remarks>
public abstract class ArchiveRoundTripState
{
    /// <summary>
    /// 初始化格式专用回写状态。
    /// </summary>
    /// <param name="codecIdentifier">建立该状态的编解码器稳定绝对 URI 标识。</param>
    /// <param name="containsUnknownContent">状态是否包含语义模型尚未解释的源内容。</param>
    /// <exception cref="ArgumentException"><paramref name="codecIdentifier"/> 不是绝对 URI。</exception>
    protected ArchiveRoundTripState(Uri codecIdentifier, bool containsUnknownContent)
    {
        ArgumentNullException.ThrowIfNull(codecIdentifier);
        if (!codecIdentifier.IsAbsoluteUri)
        {
            throw new ArgumentException("编解码器标识必须使用绝对 URI。", nameof(codecIdentifier));
        }

        CodecIdentifier = codecIdentifier;
        ContainsUnknownContent = containsUnknownContent;
    }

    /// <summary>
    /// 获取建立该状态的编解码器稳定标识。
    /// </summary>
    public Uri CodecIdentifier { get; }

    /// <summary>
    /// 获取状态是否包含语义模型尚未解释的源内容。
    /// </summary>
    public bool ContainsUnknownContent { get; }
}

/// <summary>
/// 表示语义档案及其源格式回写上下文。
/// </summary>
public sealed record ArchiveDocument
{
    /// <summary>
    /// 获取类型化档案资源包。
    /// </summary>
    public required ArchiveBundle Bundle { get; init; }

    /// <summary>
    /// 获取读取时识别到的源格式；应用新建档案时为空。
    /// </summary>
    public ArchiveFormatDescriptor? SourceFormat { get; init; }

    /// <summary>
    /// 获取编解码器提供的格式专用回写状态。
    /// </summary>
    public ArchiveRoundTripState? RoundTripState { get; init; }

    /// <summary>
    /// 获取回写状态中是否包含语义模型尚未解释的源内容。
    /// </summary>
    public bool ContainsUnknownContent => RoundTripState?.ContainsUnknownContent == true;
}

/// <summary>
/// 表示一次档案写出请求。
/// </summary>
public sealed record ArchiveWriteRequest
{
    /// <summary>
    /// 获取待写出的语义档案及回写上下文。
    /// </summary>
    public required ArchiveDocument Document { get; init; }

    /// <summary>
    /// 获取目标格式和精确版本。
    /// </summary>
    public required ArchiveFormatDescriptor TargetFormat { get; init; }
}
