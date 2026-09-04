# 账号清理

`AccountCleanupService` 提供可预览的账号筛选和删除能力，单账号删除复用 `AccountDeletionService`。管理员可以通过 HTTP 预览或执行一条规则；`MaintenanceCleanupWorker` 也会在每日维护轮次中按配置依次执行三条规则。

## 规则和时间口径

| 规则 | 配置 | 候选条件 |
| --- | --- | --- |
| 从未申请认证 | `UnsubmittedCertificationCleanupEnabled` / `CertificationSubmissionGraceDays` | 创建时间早于截止时间、没有合法角色，并且没有任何状态的认证申请 |
| 无合法角色 | `NonFormalAccountCleanupEnabled` / `NonFormalAccountRetentionDays` | 创建时间早于截止时间，并且没有合法角色；是否提交过申请不影响结果 |
| 长期未登录 | `InactiveFormalAccountCleanupEnabled` / `FormalAccountInactivityDays` | 最近成功登录时间早于截止时间；从未登录时使用创建时间，不检查角色或认证申请 |

普通注册流程不分配默认角色。只要账号存在一条关联到 Identity 角色表中现存角色的关系，就视为具有合法角色；清理逻辑不使用固定角色名称白名单。`Pending`、`Approved` 和 `Rejected` 都属于已经提交过认证申请。

长期未登录规则的历史字段名仍含 `Formal`，但当前行为会检查所有账号，管理员和其他有角色账号也不豁免。`LastSuccessfulLoginAtUtc` 目前只在密码登录成功时更新，刷新令牌和普通 HTTP 访问不会更新它，因此该规则表达的是密码登录时间，而不是最近活跃时间。启用前需要确认这一口径符合运营要求。

所有规则都使用严格早于截止时间的条件，刚好等于截止时间的对象保留。`DateTime.MinValue` 表示未知历史时间，不参与清理。

## 服务入口

```csharp
Task<AccountCleanupResult> DeleteInactiveAccountsAsync(
    DateTimeOffset cutoffUtc,
    bool dryRun = true,
    CancellationToken cancellationToken = default);

Task<AccountCleanupResult> DeleteAccountsWithoutRolesAsync(
    DateTimeOffset cutoffUtc,
    bool onlyWithoutApplications,
    bool dryRun = true,
    CancellationToken cancellationToken = default);
```

`cutoffUtc` 由调用方按当前时间和保留天数计算，不能位于未来或使用最小时间值。`onlyWithoutApplications = true` 对应“从未申请认证”，为 false 对应“无合法角色”。服务本身不读取配置、不进行授权，也不决定调度时间。

## 管理员 HTTP 接口

`AccountCleanupController` 使用管理员策略保护，并禁止响应缓存。

| 方法 | 路径 | 行为 |
| --- | --- | --- |
| `POST` | `Admin/AccountCleanup/preview` | 按当前已保存配置预览一条规则，不修改数据 |
| `POST` | `Admin/AccountCleanup/execute` | 按预览时的配置版本和截止时间执行同一规则 |

预览和手工执行不要求对应的自动开关已经开启，但规则的保留天数必须已保存为正整数。数据库中还没有 `AccountCleanup` 配置行时，接口拒绝运行。

执行请求必须携带预览取得的 `ExpectedSettingsVersion` 和 `CutoffUtc`。配置版本变化时返回 409，要求重新预览。执行时允许提交比当前配置计算结果更早的截止时间，以缩小清理范围，但不能扩大范围。

## 预览、执行和返回结果

默认 `dryRun = true`。预览只返回初筛时的完整候选清单，不修改数据库或文件。执行会冻结初筛清单，再逐账号复核相同条件；本轮开始后新出现的候选交给下一次调用处理。

`AccountCleanupResult` 包含截止时间、是否预览、条目列表、取消状态和配置变化状态。HTTP 明细只暴露账号标识、初筛用户名、状态、原因及证件文件清理失败数，不返回 Identity 实体或密码资料。

| 状态 | 含义 |
| --- | --- |
| `WouldDelete` | 初筛符合条件，尚未删除 |
| `Deleted` | 数据库中的账号及关联数据已经删除 |
| `Skipped` | 执行时账号不存在或已不再满足规则 |
| `Failed` | Identity 拒绝删除或发生无法确认成功的异常 |

数据库删除成功但证件文件删除失败时，账号仍为 `Deleted`，失败数量写入结果，遗留文件由每日孤儿文件清理补偿。一个账号失败不会停止后续账号。

## 并发、事务和取消

每个候选使用独立 DI 作用域。条件复核与数据库删除位于同一个可串行化事务中，防止角色、申请或登录时间在复核后、提交前改变。事务只覆盖单个账号，提交后才清理文件。

自动执行在账号之间复核 `AccountCleanup` 配置版本；版本变化时停止当前规则并报告 `ConfigurationChanged`。手工执行在控制器开始清理前校验配置版本，逐账号删除期间不再次读取配置。

初筛期间取消会抛出 `OperationCanceledException`。逐账号执行期间只在账号之间响应取消；已经开始的账号会完成事务、文件清理和结果记录，未处理条目保持 `WouldDelete`。

## 数量和性能边界

当前不设置批次上限，也不使用隐藏的 `Take`。候选和结果清单的内存及 HTTP 响应体积与候选数量成正比；实际删除还会为每个账号执行独立查询、事务、关联数据删除和文件操作。逐账号事务缩短了单次持锁时间，但不减少总工作量。

规模增长后可以增加后台分批和结果分页，但需要保留配置版本复核、逐账号条件复核和可审计结果，不能只在查询末尾随意增加上限。

迁移 `20260903125931_AddAccountCleanupLookupIndexes` 为认证申请和 LLM 审计的 `UserId` 增加索引，以支持复核和关联删除。SQL Server 的锁行为和大规模性能仍需在目标环境验收。
