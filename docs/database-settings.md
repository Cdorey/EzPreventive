# 数据库运行时配置

站点管理员将来通过 HTTP 修改的业务参数存入现有 ApplicationDb，由 `DatabaseSettings<T>` 负责持久化，业务服务通过标准 Options 读取。DbContext、配置存取和重载已实现；账号清理已有[独立参数方法](./account-cleanup.md)，尚未连接这些配置、HTTP 接口或自动调度。[申请超时拒绝](./certification-review.md)已接入数据库配置和自动扫描；审计过期删除仍仅有配置。

连接字符串、JWT 密钥、邮件等部署配置继续使用现有 `appsettings*.json`、环境变量或受保护配置来源。数据库配置不写回这些文件，也不自动导入同名节；一个运行时配置组只有数据库这一种持久化来源，缺少记录时使用配置类默认值。

## 数据模型

`ApplicationSettings` 每行存放一个逻辑配置组，而非一个字段或全站所有配置。

| 字段 | 含义 |
| --- | --- |
| `Key` | 稳定的配置组主键，最多 128 个 ASCII 字符；例如 `AccountCleanup` |
| `ValueJson` | 整组配置的 JSON 文档，在 SQL Server 中使用 `nvarchar(max)` |
| `SchemaVersion` | 文档结构版本，初始为 1 |
| `Version` | 应用维护的 GUID 并发令牌，每次保存重新生成 |
| `UpdatedAtUtc` | 最近修改的 UTC 时间 |
| `UpdatedByUserId` | 最近修改者标识，系统修改可为空 |

`UpdatedByUserId` 仅记录元数据，不与用户建立级联外键，删除账号不会删除站点配置。它不是完整的历史审计日志。配置表不使用 `HasData`，启动读取也不自动插入默认记录。

新增配置组只需新增 Options 类、校验器和注册。新增字段通常不需要修改表结构；结构发生变化时必须显式安排文档版本升级。当前只接受注册时声明的 `SchemaVersion`，不自动迁移旧文档；版本不兼容的程序会拒绝读取和覆盖保存。严格反序列化会拒绝未知字段、类型错误和损坏的 JSON，避免悄悄忽略配置拼写错误或新版本字段。

## 注册和使用

先注册 `ApplicationDbContext`，再注册运行时配置及纯参数校验器：

```csharp
builder.Services.AddSingleton<IValidateOptions<AccountCleanupOptions>, AccountCleanupOptionsValidator>();
builder.Services.AddDatabaseSettings<AccountCleanupOptions>(AccountCleanupOptions.SectionName);
```

扩展方法也返回 `OptionsBuilder<T>`，可继续使用 `.Validate(...)` 或 `.ValidateDataAnnotations()`。校验器在保存、加载及标准 Options 创建时使用，不应依赖请求状态或 Scoped 服务。同一配置类型和配置组标识只能注册一次；每组使用默认名称的 Options。

配置类型使用可由 `System.Text.Json` 和 `ConfigurationBinder` 一致表达的普通公共可写属性，避免自定义 JSON 属性名或在集合默认值中预置元素。一个配置组不要再叠加 `.Bind(...)`、`.Configure(...)` 或 `.PostConfigure(...)`，以免运行时值与实际保存、校验的文档不同。

`Program` 在迁移检查完成后、后台服务和 HTTP 请求开始前，调用：

```csharp
await app.Services.LoadDatabaseSettingsAsync();
```

未初始化时读取该组 Options 会明确报错。启动遇到数据库不可用、无效配置或不支持的文档版本时失败，不会装作已经成功读取默认配置。

读取遵循标准生命周期：

| 入口 | 使用方式 |
| --- | --- |
| `IOptionsMonitor<T>` | 通过 `CurrentValue` 获取当前配置，或用 `OnChange` 订阅更新；适合单例和后台服务 |
| `IOptionsSnapshot<T>` | 每个 DI Scope 缓存自己的配置，新的 Scope 读取最新值 |
| `IOptions<T>` | 沿用框架原有缓存行为，不承担动态更新 |
| `DatabaseSettings<T>.GetAsync()` | 直接从数据库读取最新文档及版本，返回可编辑副本 |

业务代码应把 Options 当作只读对象。需要编辑时从 `GetAsync()` 获取副本，并携带读取时的版本保存：

```csharp
var current = await settings.GetAsync(cancellationToken);
current.Value.SweepIntervalHours = 24;
var saved = await settings.SaveAsync(
    current.Value, current.Version, currentUserId, cancellationToken);
```

示例中的 `settings` 为 Scoped 的 `DatabaseSettings<AccountCleanupOptions>`。首次保存的 `Version` 为空，后续保存必须使用最近读取的版本；旧版本及重复创建均抛出 `DatabaseSettingsConcurrencyException`。调用方应重新读取并处理冲突，不应静默覆盖或直接用新版本重试旧表单。

保存使用独立 DbContext 和 EF 的乐观并发校验，避免附带提交当前请求中其他尚未保存的业务变更。先冻结和校验完整文档，数据库提交成功后才替换整组内存快照，再发出标准 Options 变更通知。失败的校验和数据库写入不发布新值；通知订阅者抛错会记录日志，不把已提交的保存误报为失败。保存和重载不允许加入外部 `TransactionScope`，以免发布尚未提交的配置。

## 重载和一致性

同一实例保存后立即通知 Options；`DatabaseSettingsReloadWorker` 每 30 秒加载已注册的配置组，用于同步其他实例的修改。每组独立协调和通知，版本未变化时不重复通知。后台重载失败保留上一份有效配置并记录错误，下个周期重试；直接 `GetAsync()` 仍会暴露读取或校验失败。

30 秒是配置同步周期，与各组 `SweepIntervalHours` 表达的未来业务扫描周期无关。跨实例是最终一致性：配置写入以数据库为准，普通 Options 读取不会访问数据库。今后的清理执行应在执行前通过数据库入口重新确认规则及版本；无法确认时停止执行，不能把旧内存快照当作当前配置继续处理。

所有常规写入应经过 `DatabaseSettings<T>`。受控的数据修复或文档升级也必须同时更换 `Version`；后台依靠它识别变化。不要直接修改 JSON 而保留旧版本。

## 账号清理配置

普通注册流程 `AuthManagerRepository.RegisterUserAsync` 只创建用户和可选认证申请，不分配默认角色。初始化过程预先创建 `Admin`、`Student`、`Teacher`、`Physician`、`Nutritionist`、`RD`、`Epiman`；这些是可授予的角色，并非普通注册时自动获得的角色。管理员还可以创建角色，不能把上述名字写死为唯一合法名单。

按当前需求，**存在至少一个仍然有效、且不属于注册默认角色的角色关联，才算正式账号**。目前没有需要排除的注册默认角色。认证审批和角色授予是独立操作，认证申请是否通过不作为正式账号的判据；申请记录仅用于判断非正式账号是否曾尝试认证。

`AccountCleanupOptions` 预留三档独立开关和时间参数：

这些是已有配置文档的字段。新实现的 `DeleteInactiveAccountsAsync` 已按最新需求取消角色限制，`DeleteAccountsWithoutRolesAsync` 明确从创建时间起算。接入配置调度前，需要通过显式文档版本升级调整带 `Formal` 的旧字段名称和界面含义，不能直接删除旧字段导致已保存配置无法加载。

| 账号类别 | 开关 | 时间参数 | 预期含义 |
| --- | --- | --- | --- |
| 非正式且从未提交认证申请 | `UnsubmittedCertificationCleanupEnabled` | `CertificationSubmissionGraceDays` | 注册后最短的申请宽限期 |
| 没有合法角色的非正式账号 | `NonFormalAccountCleanupEnabled` | `NonFormalAccountRetentionDays` | 较短的保留期，包含已经尝试认证的账号 |
| 拥有合法角色的正式账号 | `InactiveFormalAccountCleanupEnabled` | `FormalAccountInactivityDays` | 长期未登录后才考虑清理 |

另有 `SweepIntervalHours` 预留自动扫描间隔。三个开关默认均为 `false`，所有期限和扫描间隔默认均为空；不会擅自采用测试中的天数作为产品默认值。启用某档规则必须提供对应的正整数天数；关闭时可留空，但填写的任何时间都必须为正数。扫描间隔允许暂不填写，后续自动调度必须确认它已配置。

配置层表达“最短申请宽限、较短非正式保留、较长正式账号不活跃期限”的意图，具体天数、非正式账号计时起点、分档优先级及管理员保护留到清理策略阶段。各档时间尚不做跨字段大小比较，因为它们的计时基准不同。

后续执行策略还需明确：

- 角色被撤销后如何重新计算非正式账号的保留期，以及保护哪些角色或系统账号。
- 待审核、被拒绝、重复申请如何影响保留期；提交过申请的账号不再属于“从未尝试”一档。
- 正式账号的“未登录”如何统计：当前 `LastSuccessfulLoginAtUtc` 只在密码登录时更新，刷新令牌不更新，持续使用长会话的用户不能因此被误判。
- 历史账号 `CreatedAtUtc` 缺省时间、无登录记录以及预览到执行之间的角色、申请、登录变化如何处理。

## 申请超时和 LLM 审计清理配置

除账号配置外，另外注册两个独立配置组，各自存为 `ApplicationSettings` 的一行，复用相同的持久化、并发校验和 Options 重载机制，不需要新增表或修改账号配置的文档版本。

| 配置类／配置组 | 开关 | 天数参数 | 预期动作 |
| --- | --- | --- | --- |
| `CertificationRequestCleanupOptions`／`CertificationRequestCleanup` | `AutoRejectEnabled` | `PendingTimeoutDays` | 自动拒绝超过指定天数未获管理员处理的待审核认证申请 |
| `LlmAuditCleanupOptions`／`LlmAuditCleanup` | `Enabled` | `RetentionDays` | 删除超过保留期限的 LLM 调用审计记录 |

每组还有自己的 `SweepIntervalHours`，允许将来采用不同的扫描频率。默认开关关闭，天数和扫描间隔均为空；启用时必须配置对应的正整数天数，任何已填写的间隔也必须为正数。示例测试中的 14 天、90 天等不是产品默认值。

申请超时处理将符合条件的 `Pending` 申请转为 `Rejected`，保留申请历史和已有角色。超时从 `RequestTime` 起算，备注修改不会延长期限；只有通过或拒绝时才设置 `ProcessedTime`。自动扫描需要 `AutoRejectEnabled = true` 且已配置 `PendingTimeoutDays` 和 `SweepIntervalHours`；间隔为空时不启动扫描。后台每分钟读取数据库中的最新配置判断是否到期，每条申请开始前复核配置版本，变化则停止本轮。申请版本保护、有限重试和图片清理见[认证审核说明](./certification-review.md)。

当前 LLM 审计对应 `PrescriptionGenerateRequests`。生成前先写入 `RequestTime`，在结束、取消或失败的收尾阶段写入 `ProcessedTime` 和结果；进程中断或收尾保存失败的记录可能仍保留默认的完成时间。后续需明确保留期从请求开始还是完成起算，并区分运行中的请求和异常未完成记录，不能直接把默认完成时间当作过期依据。现有孤儿审计清理仅按所属账号是否存在判定，与这里按时间过期的清理是独立规则。

## 数据库发布与验证

迁移 `20260903121909_AddApplicationSettings` 只为 ApplicationDb 新增配置表，不修改用户、认证申请、角色或营养参考数据，不插入默认配置。发布前按现有流程审阅并应用迁移；启动默认只检查待执行迁移。回滚迁移会删除配置表及其中数据，应先按部署要求备份。

在仓库根目录运行：

```powershell
dotnet test Tests/EzNutrition.Server.Tests/EzNutrition.Server.Tests.csproj
dotnet ef migrations has-pending-model-changes --project EzNutrition.Server --context ApplicationDbContext
```

配置测试使用内存 SQLite，验证默认值、持久化与重启、标准 Options 生命周期、并发更新和首次创建竞争、失败时不发布、跨实例同步、后台定时重载以及无效文档处理；不会连接部署环境数据库。
