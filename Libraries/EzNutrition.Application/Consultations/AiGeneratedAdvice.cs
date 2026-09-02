using EzNutrition.Shared.Data.DTO.PromptDto;

namespace EzNutrition.Application.Consultations;

/// <summary>
/// 指定当前 AI 营养建议的运行态生成状态。
/// </summary>
public enum AiAdviceGenerationStatus
{
    /// <summary>已准备输入。</summary>
    Prepared = 0,

    /// <summary>正在生成。</summary>
    Generating = 1,

    /// <summary>已获得完整建议。</summary>
    Completed = 2,

    /// <summary>生成已中断。</summary>
    Incomplete = 3,

    /// <summary>生成失败。</summary>
    Failed = 4
}

/// <summary>
/// 保存一次 AI 营养建议生成过程的运行态内容。
/// </summary>
public sealed class AiGeneratedAdvice
{
    internal Guid? GenerationAttemptId { get; set; }

    /// <summary>获取或设置结果是否可供正式复核。</summary>
    public bool IsReady { get; set; }

    /// <summary>获取或设置请求是否仍在发送或接收。</summary>
    public bool Sending { get; set; }

    /// <summary>获取或设置推理内容。</summary>
    public string ReasoningContent { get; set; } = string.Empty;

    /// <summary>获取或设置建议正文。</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>获取或设置结构化生成状态。</summary>
    public AiAdviceGenerationStatus GenerationStatus { get; set; } = AiAdviceGenerationStatus.Prepared;

    /// <summary>获取或设置请求开始时间。</summary>
    public DateTimeOffset? RequestedAt { get; set; }

    /// <summary>获取或设置生成完成或中断时间。</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>获取或设置生成环境信息。</summary>
    public EnvironmentDto? Environment { get; set; }
}
