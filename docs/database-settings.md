# 数据库运行时配置

管理员可以在服务运行期间修改的维护参数存入现有 `ApplicationDb`，由 `DatabaseSettings<T>` 按配置组持久化，并通过 ASP.NET Core 标准 Options 接口发布。连接字符串、JWT 密钥、邮件和外部服务凭据等部署配置继续来自 `appsettings*.json`、环境变量或受保护的配置源；数据库配置不会写回或覆盖这些文件。

账号清理、认证申请超时拒绝、LLM 审计保留期和每日清理时间已经接入该机制。清理任务的执行顺序和运行边界见[维护清理任务](./maintenance-cleanup.md)。

## 数据模型

`ApplicationSettings` 每行保存一个完整的逻辑配置组。

| 字段 | 含义 |
| --- | --- |
| `Key` | 稳定的配置组主键，最多 128 个 ASCII 字符 |
| `ValueJson` | 整组配置的 JSON 文档 |
| `SchemaVersion` | 配置文档结构版本，当前各组为 1 |
| `Version` | GUID 并发令牌，每次保存重新生成 |
| `UpdatedAtUtc` | 最近修改的 UTC 时间 |
| `UpdatedByUserId` | 最近修改者；系统修改可为空 |

`UpdatedByUserId` 只记录最近一次修改的元数据，不建立用户外键，也不是完整审计日志。缺少配置行时，系统使用对应 Options 类的默认值，但不会自动插入数据库记录。

配置 JSON 使用严格反序列化：未知字段、类型错误、损坏的 JSON、无效并发版本或不支持的结构版本都会使加载失败。新增兼容字段通常不需要修改表结构；删除或改名字段必须先设计配置文档迁移，并提高 `SchemaVersion`，不能直接依赖反序列化忽略旧数据。

## 注册、加载和读取

配置组在 `Program` 中注册。需要业务校验的配置先注册无状态的 `IValidateOptions<T>`：

```csharp
builder.Services.AddSingleton<IValidateOptions<AccountCleanupOptions>, AccountCleanupOptionsValidator>();
builder.Services.AddDatabaseSettings<AccountCleanupOptions>(AccountCleanupOptions.SectionName);
```

数据库迁移检查完成后，应用在启动 HTTP 管线前加载全部配置：

```csharp
await app.Services.LoadDatabaseSettingsAsync();
```

数据库不可用、持久化配置无效或结构版本不受支持时，应用启动失败。加载成功后可以使用标准 Options 生命周期：

| 入口 | 行为 |
| --- | --- |
| `IOptionsMonitor<T>` | 获取当前快照并订阅变更，适合单例和后台服务 |
| `IOptionsSnapshot<T>` | 每个 DI 作用域缓存一份快照 |
| `IOptions<T>` | 使用框架的普通缓存语义 |
| `DatabaseSettings<T>.GetAsync()` | 直接读取数据库中的最新文档、版本和修改元数据 |

业务代码应把 Options 值当作只读快照。修改配置时先调用 `GetAsync()` 获得副本，再携带其版本保存完整配置组：

```csharp
var current = await settings.GetAsync(cancellationToken);
current.Value.NonFormalAccountRetentionDays = 30;
var saved = await settings.SaveAsync(
    current.Value,
    current.Version,
    currentUserId,
    cancellationToken);
```

首次保存时版本为空；已有配置必须提交最近读取的版本。版本过期或两个写入者同时首次创建时抛出 `DatabaseSettingsConcurrencyException`，调用方需要重新读取，不应使用旧表单静默覆盖。

保存使用独立 DbContext 和乐观并发校验。完整文档通过校验且数据库提交成功后，才替换内存快照并发出 Options 变更通知。保存和重载不能加入外部 `TransactionScope`。

## HTTP 接口

`MaintenanceSettingsController` 使用管理员策略保护，并禁止响应缓存。

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| `GET` | `Admin/MaintenanceSettings` | 读取全部维护配置及各组版本 |
| `PUT` | `Admin/MaintenanceSettings/cleanup-schedule` | 保存每日清理时间 |
| `PUT` | `Admin/MaintenanceSettings/account-cleanup` | 保存账号清理配置 |
| `PUT` | `Admin/MaintenanceSettings/certification-request-cleanup` | 保存认证申请清理配置 |
| `PUT` | `Admin/MaintenanceSettings/llm-audit-cleanup` | 保存 LLM 审计清理配置 |

PUT 请求提交完整的 `Value` 和读取时获得的 `ExpectedVersion`。成功返回保存后的配置和新版本；业务校验失败返回 400，版本冲突返回 409。

## 重载和多实例一致性

当前实例通过 `DatabaseSettings<T>.SaveAsync()` 保存后立即发布新快照。`DatabaseSettingsReloadWorker` 每 30 秒从数据库同步其他实例提交的变更；版本不变时不重复通知。重载失败会保留上一份已验证快照并记录错误，下个周期重试。

这种同步是最终一致的。清理服务在执行期间还会复核数据库配置版本：账号和认证申请按条目检查，LLM 审计清理在可串行化事务中检查。版本变化时停止或跳过本轮剩余工作，下一次每日任务重新计算截止时间。

## 当前配置组和默认值

| 配置组 | 字段 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `CleanupSchedule` | `StartTime` | `03:30` | 每日启动维护清理的服务器本地时间 |
| `AccountCleanup` | 三个清理开关 | `false` | 分别控制从未申请、无合法角色和长期未登录账号清理 |
| `AccountCleanup` | 三个天数参数 | `null` | 对应规则的保留期；启用规则时必须为正整数 |
| `CertificationRequestCleanup` | `AutoRejectEnabled` | `false` | 是否自动拒绝超时待审核申请 |
| `CertificationRequestCleanup` | `PendingTimeoutDays` | `null` | 从 `RequestTime` 起算的超时天数 |
| `LlmAuditCleanup` | `Enabled` | `false` | 是否删除过期 LLM 审计记录 |
| `LlmAuditCleanup` | `RetentionDays` | `null` | 从 `RequestTime` 起算的保留天数 |

未启用的规则允许天数为空；任何已填写的天数都必须为正整数。测试中使用的天数不是产品默认值。`CleanupSchedule` 当前不需要额外校验器，因为 `TimeOnly` 已限制其表示范围。

账号规则的精确定义见[账号清理](./account-cleanup.md)，申请状态和并发规则见[认证申请审核与超时拒绝](./certification-review.md)。

## 数据库发布与验证

迁移 `20260903121909_AddApplicationSettings` 新增配置表，不插入默认配置。回滚会删除配置表及其中数据，部署前应按环境要求备份并审阅迁移。

```powershell
dotnet test Tests/EzNutrition.Server.Tests/EzNutrition.Server.Tests.csproj
dotnet ef migrations has-pending-model-changes --project EzNutrition.Server --context ApplicationDbContext
```

配置测试使用隔离 SQLite，覆盖默认值、持久化与重启、Options 生命周期、并发写入、失败时不发布、跨实例同步和无效文档处理，不连接部署环境数据库。
