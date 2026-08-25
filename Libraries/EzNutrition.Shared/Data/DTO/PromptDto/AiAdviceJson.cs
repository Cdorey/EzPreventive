using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EzNutrition.Shared.Data.DTO.PromptDto;

/// <summary>
/// 提供 AI 咨询数据在传输与披露界面中共用的 JSON 表示。
/// </summary>
public static class AiAdviceJson
{
    /// <summary>获取用于 HTTP 传输和模型数据消息的紧凑 JSON 选项。</summary>
    public static JsonSerializerOptions Compact { get; } = Create(writeIndented: false);

    /// <summary>获取用于医生发送前预览的缩进 JSON 选项。</summary>
    public static JsonSerializerOptions Indented { get; } = Create(writeIndented: true);

    /// <summary>Serializes AI consultation data using the shared compact representation.</summary>
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Compact);

    private static JsonSerializerOptions Create(bool writeIndented) => new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = writeIndented
    };
}
