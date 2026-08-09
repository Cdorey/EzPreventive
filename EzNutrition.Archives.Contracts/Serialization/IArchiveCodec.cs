namespace EzNutrition.Archives.Contracts.Serialization;

/// <summary>
/// 定义类型化档案与外部编码格式之间的转换边界。
/// </summary>
/// <remarks>
/// 实现可以使用 XML、JSON 或其他格式；接口不负责文档最终存放位置。
/// </remarks>
public interface IArchiveCodec
{
    /// <summary>
    /// 获取编解码器的稳定绝对 URI 标识。
    /// </summary>
    Uri CodecIdentifier { get; }

    /// <summary>
    /// 获取实现能够读取的格式和版本稳定快照。
    /// </summary>
    IReadOnlyCollection<ArchiveFormatDescriptor> ReadableFormats { get; }

    /// <summary>
    /// 获取实现能够写出的格式和版本稳定快照。
    /// </summary>
    IReadOnlyCollection<ArchiveFormatDescriptor> WritableFormats { get; }

    /// <summary>
    /// 从受调用方控制的流读取、验证并迁移档案。
    /// </summary>
    /// <param name="source">可读输入流。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>读取结果。</returns>
    ValueTask<ArchiveReadResult> ReadAsync(
        Stream source,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 将当前类型化档案写出为指定格式版本。
    /// </summary>
    /// <param name="request">待写出的档案、回写上下文和目标格式。</param>
    /// <param name="destination">可写输出流。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>写出结果。</returns>
    /// <remarks>
    /// 请求携带未知源内容时，编解码器应完整保留该内容，或以兼容性错误结束写出。
    /// </remarks>
    ValueTask<ArchiveWriteResult> WriteAsync(
        ArchiveWriteRequest request,
        Stream destination,
        CancellationToken cancellationToken = default);
}
