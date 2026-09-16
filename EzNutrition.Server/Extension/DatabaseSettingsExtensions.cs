using EzNutrition.Server.Data;
using EzNutrition.Server.Services.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EzNutrition.Server.Extension;

/// <summary>注册以 ApplicationDb 为持久化来源的强类型运行时配置。</summary>
public static class DatabaseSettingsExtensions
{
    /// <summary>注册一个独立配置组；应先注册 ApplicationDbContext，并在启动服务前加载配置。</summary>
    /// <typeparam name="T">可由标准 ConfigurationBinder 绑定的配置类型。</typeparam>
    /// <param name="services">应用服务集合。</param>
    /// <param name="key">稳定的 ASCII 配置组标识，不随类型重命名而变化。</param>
    /// <param name="schemaVersion">当前支持的文档结构版本。</param>
    public static OptionsBuilder<T> AddDatabaseSettings<T>(
        this IServiceCollection services, string key, int schemaVersion = 1) where T : class, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (key.Length > 128 || key.Any(character =>
            !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not '-'))
        {
            throw new ArgumentException("配置组标识只能包含 ASCII 字母、数字、点、下划线和连字符，长度不超过 128。", nameof(key));
        }
        ArgumentOutOfRangeException.ThrowIfLessThan(schemaVersion, 1);
        if (services.Any(service => service.ServiceType == typeof(DatabaseOptions<T>)))
        {
            throw new InvalidOperationException($"配置类型 {typeof(T).Name} 已注册数据库来源。");
        }

        services.AddDbContextFactory<ApplicationDbContext>(lifetime: ServiceLifetime.Scoped);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(provider => new DatabaseOptions<T>(key, schemaVersion,
            provider.GetServices<IValidateOptions<T>>(), provider.GetRequiredService<ILogger<DatabaseOptions<T>>>()));
        services.AddSingleton<IConfigureOptions<T>>(provider => provider.GetRequiredService<DatabaseOptions<T>>());
        services.AddSingleton<IOptionsChangeTokenSource<T>>(provider => provider.GetRequiredService<DatabaseOptions<T>>());
        services.AddScoped<DatabaseSettings<T>>();
        services.AddScoped<IDatabaseSettingsLoader>(provider => provider.GetRequiredService<DatabaseSettings<T>>());
        services.AddHostedService<DatabaseSettingsReloadWorker>();
        return services.AddOptions<T>().ValidateOnStart();
    }

    /// <summary>数据库迁移完成后、后台服务和 HTTP 请求开始前，加载所有已注册的配置组。</summary>
    public static async Task LoadDatabaseSettingsAsync(
        this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var loaders = scope.ServiceProvider.GetServices<IDatabaseSettingsLoader>().ToArray();
        if (loaders.Select(loader => loader.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != loaders.Length)
        {
            throw new InvalidOperationException("数据库配置组标识不能重复注册。");
        }
        foreach (var loader in loaders)
        {
            await loader.ReloadAsync(cancellationToken);
        }
    }
}
