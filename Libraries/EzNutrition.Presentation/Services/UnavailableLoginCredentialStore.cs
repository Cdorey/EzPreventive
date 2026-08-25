namespace EzNutrition.Presentation.Services;

/// <summary>
/// 表示宿主没有登录信息持久化能力。
/// </summary>
internal sealed class UnavailableLoginCredentialStore : ILoginCredentialStore
{
    /// <summary>获取可复用的无持久化实现。</summary>
    internal static UnavailableLoginCredentialStore Instance { get; } = new();

    private UnavailableLoginCredentialStore()
    {
    }

    /// <inheritdoc />
    public bool IsAvailable => false;

    /// <inheritdoc />
    public ValueTask<SavedLoginCredential?> ReadAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<SavedLoginCredential?>(null);

    /// <inheritdoc />
    public ValueTask SaveAsync(
        SavedLoginCredential credential,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("当前宿主不支持保存登录信息。");

    /// <inheritdoc />
    public ValueTask ClearAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
