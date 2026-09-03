using EzNutrition.Shared.Data.DTO;

namespace EzNutrition.Presentation.Services;

/// <summary>宿主负责刷新凭据的传递与保存，共享层只管理会话行为和短期访问令牌。</summary>
public interface IAuthenticationSessionClient
{
    /// <summary>获取宿主是否支持用户选择保持登录。</summary>
    bool CanRememberLogin { get; }

    /// <summary>使用账号密码登录，并按用户选择保存登录会话。</summary>
    Task<AuthenticationTokensDto> SignInAsync(
        LoginRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>恢复宿主保存的会话；没有保存凭据时返回空值。</summary>
    Task<AuthenticationTokensDto?> RestoreAsync(CancellationToken cancellationToken = default);

    /// <summary>刷新指定会话；共享凭据已被其他账号替换时必须拒绝。</summary>
    Task<AuthenticationTokensDto> RefreshAsync(
        Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>撤销预期会话并清除恢复凭据；空标识表示退出正在恢复的会话。</summary>
    Task SignOutAsync(Guid? sessionId, CancellationToken cancellationToken = default);
}

/// <summary>表示服务器明确拒绝认证；网络故障不应转换成此异常。</summary>
public sealed class SessionAuthenticationException : InvalidOperationException
{
    /// <summary>创建具有稳定错误码的认证异常。</summary>
    public SessionAuthenticationException(string code, string message) : base(message)
    {
        Code = code;
    }

    /// <summary>获取认证错误码。</summary>
    public string Code { get; }
}
