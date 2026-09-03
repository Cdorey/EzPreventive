# 账号清理服务

`AccountCleanupService` 提供两个可预览的参数化入口。它负责候选筛选和执行协调，单账号删除复用 `AccountDeletionService`；当前尚未连接 HTTP、站点配置或自动调度，不会因启动服务而删除账号。

## 两种筛选

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

`cutoffUtc` 是整轮固定的截止时间，由调用方使用时钟和保留天数计算。接受带时区偏移的时间，返回时统一为 UTC；不接受未来时间或最小时间值。所有年龄判断均为严格早于截止时间，刚好处于边界的账号保留。

- **长期未登录**：使用 `LastSuccessfulLoginAtUtc`，为空时回退到 `CreatedAtUtc`。最终采用的时间为最小值时视为未知，不参与清理。不检查角色和申请，因此管理员角色也不构成豁免。当前登录字段只记录密码登录，刷新会话不更新；此方法表达的是密码登录时间口径，并非最近 HTTP 访问时间。
- **创建已久且无合法角色**：要求有效创建时间早于截止时间，且不存在关联到 Identity 角色表的角色。当前普通注册没有默认角色，也不对合法角色名称设置白名单。`onlyWithoutApplications = true` 时还要求没有任何认证申请；Pending、Approved、Rejected 都算申请过。为 false 时完全忽略申请记录。

第二个入口通过不同截止时间和申请开关覆盖非正式账号的两个保留档位。它按账号创建时间计算，不根据最近申请、拒绝或角色撤销时间延长保留期。

## 预览和返回清单

默认 `dryRun = true`，只读取初筛时符合条件的账号，不修改数据库、文件或配置。执行模式处理初筛清单中的全部账号，不设置最大数量；本轮开始后新出现的候选交由下次调用处理。

`AccountCleanupResult` 包含 `CutoffUtc`、`DryRun`、`Items` 和 `IsCanceled`。明细只包含账号主键、初筛时的用户名、状态、原因以及证件文件清理失败数量，不返回 Identity 实体、密码散列或其他账号资料。

| 状态 | 含义 |
| --- | --- |
| `WouldDelete` | 初筛符合条件，尚未执行；用于预览或取消后剩余的条目 |
| `Deleted` | 账号已经成功从数据库删除 |
| `Skipped` | 执行时账号已不存在，或角色、申请、时间条件已不满足 |
| `Failed` | Identity 拒绝删除或发生异常，操作未能确认成功；详细异常仅记录在服务端日志 |

数据库删除成功但证件文件删除失败时，状态仍为 `Deleted`，并返回 `CertificateFileCleanupFailures` 和补偿清理提示。单个账号失败后继续处理后续账号，各账号之间不共享删除事务。

预览不是锁定账号的承诺，执行会重新查询。用户名只代表初筛时的显示名称，稳定标识始终是 `UserId`。

## 复核、事务和取消

初筛仅投影账号主键和用户名，不跟踪实体，也不在逐账号执行期间保持打开的候选查询读取器。每个候选使用独立 DI 作用域，避免共用 `AccountDeletionService` 会清空的 DbContext 跟踪器。

条件复核与数据库删除位于同一个 Serializable 事务，防止复核完成后、删除提交前的登录时间、角色关系或申请记录变化使条件失效。事务只覆盖单账号的数据库操作，提交后才清理文件。并发运行可能产生阻塞或数据库死锁；失败反映在对应账号结果中，不做无条件重试。更高隔离级别对并发的影响参见 [EF Core 并发文档](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)。

取消在两个阶段具有不同含义：

- 初筛期间取消会抛出 `OperationCanceledException`，此时尚未开始删除。
- 进入逐账号执行后，在账号之间检查取消。已经开始的单账号操作会完成事务、文件清理及结果记录，再停止处理后续账号；返回 `IsCanceled = true` 和完整候选清单，未处理项保留 `WouldDelete`。如果最后一个账号已经完成，则整轮视为完成。

当前方法不加入外部 `TransactionScope`。它也不负责授权或读取配置；后续 HTTP 与调度层需要先验证调用权限，并读取、核实最新持久化配置，再计算本次参数。

## 不限制总数量的代价

一次初筛返回全部候选，完整结果清单需要与候选数量成正比的内存，未来 HTTP 响应的体积同样增长。只读取必要字段可以减小常数开销，但不能消除完整 List 的内存成本；参见 [EF Core 查询投影与缓冲说明](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying)。

实际删除需要逐账号执行多次 SQL 和一个事务，还要处理各账号的关联记录和文件，总耗时取决于账号数量、关联数据规模和数据库往返延迟。逐账号事务避免整轮一直持有数据库锁，但不会减少总体删除量和日志写入量。不应由内存测试的耗时推断生产环境的吞吐量。

规模变大后，可将执行放入后台任务、对结果分页展示；这与限制本轮允许清理的总数量是不同的设计。本次没有隐藏的 Take 上限，也没有设置批次参数。

## 索引与验证

迁移 `20260903125931_AddAccountCleanupLookupIndexes` 为认证申请和 LLM 审计的 `UserId` 增加索引，支撑逐账号的复核和关联删除。列长度对齐 Identity 主键的 450 个 UTF-16 代码单元；升级前先检查历史超长值，存在时拒绝迁移，不截断标识。迁移不增加外键，也不删除孤儿数据。索引占用额外存储，并增加对应记录写入时的维护成本。

部署前应审阅并按既有流程应用迁移。本次开发没有连接或清理实际站点数据库。SQLite 测试覆盖边界时间、角色与申请筛选、预览不写入、初筛后的状态变化、事务隔离级别、关联删除回滚、文件失败结果、取消和完整清单；Windows 文件共享失败用例在其他平台明确跳过。SQL Server 的实际锁行为与大规模性能仍需在目标环境验收。
