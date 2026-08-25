namespace EzNutrition.Presentation.Services;

/// <summary>
/// 描述宿主可选提供的登录信息持久化能力。
/// </summary>
/// <remarks>
/// Presentation 只依赖此端口，不约定保存位置或保护机制。浏览器宿主默认不提供实现，
/// Windows 宿主可以使用当前用户范围的数据保护机制实现。
/// </remarks>
public interface ILoginCredentialStore
{
    /// <summary>获取当前宿主是否允许用户保存登录信息。</summary>
    bool IsAvailable { get; }

    /// <summary>读取当前服务连接范围内保存的登录信息。</summary>
    ValueTask<SavedLoginCredential?> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>保存当前服务连接范围内的登录信息。</summary>
    ValueTask SaveAsync(
        SavedLoginCredential credential,
        CancellationToken cancellationToken = default);

    /// <summary>清除当前服务连接范围内保存的登录信息。</summary>
    ValueTask ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 表示宿主安全存储边界中读取的一组登录信息。
/// </summary>
public sealed class SavedLoginCredential
{
    /// <summary>
    /// 创建一组经过基本校验的登录信息。
    /// </summary>
    /// <param name="userName">用户名。</param>
    /// <param name="password">密码。</param>
    public SavedLoginCredential(string userName, string password)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new ArgumentException("用户名不能为空。", nameof(userName));
        }

        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("密码不能为空。", nameof(password));
        }

        UserName = userName.Trim();
        Password = password;
    }

    /// <summary>获取用户名。</summary>
    public string UserName { get; }

    /// <summary>获取密码。</summary>
    public string Password { get; }
}
