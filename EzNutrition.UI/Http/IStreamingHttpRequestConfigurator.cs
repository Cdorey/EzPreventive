namespace EzNutrition.UI.Http;

/// <summary>
/// 为当前 UI 宿主配置需要流式读取响应的 HTTP 请求。
/// </summary>
public interface IStreamingHttpRequestConfigurator
{
    /// <summary>
    /// 启用当前宿主支持的响应流式读取能力。
    /// </summary>
    /// <param name="request">即将发送的 HTTP 请求。</param>
    void EnableResponseStreaming(HttpRequestMessage request);
}
