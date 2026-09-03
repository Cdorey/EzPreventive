using EzNutrition.Server.Data;
using EzNutrition.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Transactions;

namespace EzNutrition.Server.Services.Settings;

/// <summary>提供配置的异步持久化入口，数据库提交成功后才发布 Options 变更。</summary>
/// <typeparam name="T">已注册的强类型配置。</typeparam>
public sealed class DatabaseSettings<T>(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    DatabaseOptions<T> options,
    TimeProvider timeProvider) : IDatabaseSettingsLoader where T : class, new()
{
    /// <inheritdoc />
    public string Key => options.Key;

    /// <summary>从数据库读取供编辑或执行前复核的最新配置；不存在记录时返回默认值及空版本。</summary>
    public async Task<DatabaseSettingsValue<T>> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var setting = await context.ApplicationSettings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Key == Key, cancellationToken);
        return DatabaseOptions<T>.ToValue(options.Prepare(setting));
    }

    /// <summary>校验并保存整组配置；独立 DbContext 避免提交调用方尚未保存的其他业务修改。</summary>
    /// <param name="value">待保存的完整配置。</param>
    /// <param name="expectedVersion">读取时返回的版本；只有首次创建记录时才传空值。</param>
    /// <param name="updatedByUserId">修改者标识，仅作为审计元数据保存。</param>
    /// <param name="cancellationToken">取消信号。</param>
    /// <exception cref="DatabaseSettingsConcurrencyException">数据库版本已发生变化。</exception>
    public async Task<DatabaseSettingsValue<T>> SaveAsync(
        T value, Guid? expectedVersion, string? updatedByUserId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (expectedVersion == Guid.Empty)
        {
            throw new ArgumentException("并发版本不能使用空 GUID。", nameof(expectedVersion));
        }
        if (updatedByUserId?.Length > 450)
        {
            throw new ArgumentException("修改者标识不能超过 450 个字符。", nameof(updatedByUserId));
        }
        EnsureNoAmbientTransaction();

        var setting = new ApplicationSetting
        {
            Key = Key,
            ValueJson = options.Serialize(value),
            SchemaVersion = options.SchemaVersion,
            Version = Guid.NewGuid(),
            UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            UpdatedByUserId = updatedByUserId
        };
        var snapshot = options.Prepare(setting);
        ConfigurationReloadToken? changeToken;
        await options.UpdateGate.WaitAsync(cancellationToken);
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var current = await context.ApplicationSettings.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Key == Key, cancellationToken);
            if (current?.Version != expectedVersion)
            {
                throw new DatabaseSettingsConcurrencyException(Key);
            }
            if (current is not null && current.SchemaVersion != options.SchemaVersion)
            {
                throw new InvalidDataException($"配置组 {Key} 的文档版本不受支持，不能覆盖保存。");
            }

            if (current is null)
            {
                context.ApplicationSettings.Add(setting);
            }
            else
            {
                var entry = context.Attach(setting);
                entry.State = EntityState.Modified;
                entry.Property(item => item.Version).OriginalValue = current.Version;
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                throw new DatabaseSettingsConcurrencyException(Key, exception);
            }
            catch (DbUpdateException exception) when (current is null)
            {
                // 首次插入也可能与其他实例竞争；仅确有同名记录时转换为并发冲突。
                if (await context.ApplicationSettings.AsNoTracking()
                    .AnyAsync(item => item.Key == Key, cancellationToken))
                {
                    throw new DatabaseSettingsConcurrencyException(Key, exception);
                }
                throw;
            }

            changeToken = options.Publish(snapshot);
        }
        finally
        {
            options.UpdateGate.Release();
        }

        options.Notify(changeToken);
        return DatabaseOptions<T>.ToValue(snapshot);
    }

    /// <inheritdoc />
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        EnsureNoAmbientTransaction();
        ConfigurationReloadToken? changeToken;
        await options.UpdateGate.WaitAsync(cancellationToken);
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var setting = await context.ApplicationSettings.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Key == Key, cancellationToken);
            changeToken = options.Publish(options.Prepare(setting));
        }
        finally
        {
            options.UpdateGate.Release();
        }

        options.Notify(changeToken);
    }

    /// <summary>发布操作不能依赖尚未提交的外部事务，避免向其他请求暴露未持久化的配置。</summary>
    private static void EnsureNoAmbientTransaction()
    {
        if (Transaction.Current is not null)
        {
            throw new InvalidOperationException("配置保存和重载不能加入外部环境事务。");
        }
    }
}
