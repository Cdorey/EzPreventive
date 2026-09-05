# 维护配置与账号清理

[返回 HTTP API 目录](./README.md)。本页所有接口要求管理员角色 `Admin`，响应禁止缓存。

## 配置读取与保存

`GET /Admin/MaintenanceSettings` 返回四组配置：

```json
{
  "cleanupSchedule": { "value": { "startTime": "03:30:00" }, "version": null, "schemaVersion": 1, "updatedAtUtc": null, "updatedByUserId": null },
  "accountCleanup": { "value": { "unsubmittedCertificationCleanupEnabled": false, "certificationSubmissionGraceDays": null, "nonFormalAccountCleanupEnabled": false, "nonFormalAccountRetentionDays": null, "inactiveFormalAccountCleanupEnabled": false, "formalAccountInactivityDays": null }, "version": null, "schemaVersion": 1, "updatedAtUtc": null, "updatedByUserId": null },
  "certificationRequestCleanup": { "value": { "autoRejectEnabled": false, "pendingTimeoutDays": null }, "version": null, "schemaVersion": 1, "updatedAtUtc": null, "updatedByUserId": null },
  "llmAuditCleanup": { "value": { "enabled": false, "retentionDays": null }, "version": null, "schemaVersion": 1, "updatedAtUtc": null, "updatedByUserId": null }
}
```

上例是尚未保存任何配置时的默认响应。读取不会自动创建配置；保存后 `version` 为 GUID，修改时间为 UTC，修改者为账号 ID。`schemaVersion` 是配置结构版本，不用于代替并发版本。

| 保存路径（均为 PUT） | `value` 对应的配置组 |
| --- | --- |
| `/Admin/MaintenanceSettings/cleanup-schedule` | `cleanupSchedule` |
| `/Admin/MaintenanceSettings/account-cleanup` | `accountCleanup` |
| `/Admin/MaintenanceSettings/certification-request-cleanup` | `certificationRequestCleanup` |
| `/Admin/MaintenanceSettings/llm-audit-cleanup` | `llmAuditCleanup` |

每次提交**完整配置组**，不是 PATCH。例如首次保存每日时间：

```json
{ "value": { "startTime": "03:30:00" }, "expectedVersion": null }
```

后续保存把 `expectedVersion` 换为上次读取或保存取得的版本。成功为 200，返回该组的 `{value,version,schemaVersion,updatedAtUtc,updatedByUserId}`，不返回其余配置组。校验失败返回 400 验证响应；版本过期返回 409 `{key,message}`，应重新读取并让操作者核对新值，不能用旧表单自动覆盖。

`startTime` 为服务器本地每日时间；没有 `sweepIntervalHours`。所有已填写的天数必须是正整数，即使开关关闭也一样；启用某条规则必须填写对应天数。每日时间影响自动任务，手工预览和执行不必等待该时间。调度重启与配置同步细节见[维护任务实现](../maintenance-cleanup.md)及[配置实现](../database-settings.md)。

## 账号规则

| `rule` | 含义 | 使用的天数 |
| --- | --- | --- |
| 0 | 创建已超期、无合法角色、从未提交任何状态的认证申请 | `certificationSubmissionGraceDays` |
| 1 | 创建已超期、无合法角色；不论是否申请 | `nonFormalAccountRetentionDays` |
| 2 | 最近成功密码登录已超期，从未登录则用创建时间；**包括管理员及其他有角色账号** | `formalAccountInactivityDays` |

任一现存角色都算合法角色，不按角色名称白名单判断；普通注册不分配默认角色。申请被拒绝也算“曾经申请”。刷新令牌和普通访问不算密码登录。未知历史时间排除，只有严格早于截止时间才成为候选。

## 预览与执行

`POST /Admin/AccountCleanup/preview`：

```json
{ "rule": 0 }
```

成功返回 200：

```json
{
  "rule": 0,
  "settingsVersion": "11111111-1111-4111-8111-111111111111",
  "cutoffUtc": "2026-09-01T00:00:00+00:00",
  "dryRun": true,
  "items": [ { "userId": "example-user", "userName": "example", "status": 0, "reason": "...", "certificateFileCleanupFailures": 0 } ],
  "isCanceled": false
}
```

`POST /Admin/AccountCleanup/execute` 携带预览结果：

```json
{
  "rule": 0,
  "expectedSettingsVersion": "11111111-1111-4111-8111-111111111111",
  "cutoffUtc": "2026-09-01T00:00:00+00:00"
}
```

成功响应结构相同，`dryRun` 为 false。两种操作都要求账号清理配置已保存且对应天数为正整数，**不要求自动清理开关打开**。无效规则、配置缺失、天数未设置或不允许的截止时间返回 400；执行时配置版本不匹配返回 409 `{key,message}`。

截止时间通常为预览时的当前 UTC 减去保留天数。执行允许使用更早的截止时间缩小范围，但不能晚于执行时按当前保留期计算的时间，也不能使用最小日期。执行重新查询候选并逐条复核，**预览不是保留或锁定的待删除 ID 清单**；两次请求之间符合条件的账号集合可能变化。手工执行开始时校验配置版本，已开始的删除不会因随后保存配置自动取消。

| 条目 `status` | 含义 |
| --- | --- |
| 0 `WouldDelete` | 候选，尚未执行删除；预览或取消后未处理条目 |
| 1 `Deleted` | 账号及关联数据库数据已删除 |
| 2 `Skipped` | 账号不存在或已经不符合条件 |
| 3 `Failed` | 删除失败或无法确认成功，查看 `reason` |

200 不代表所有条目删除成功。`Deleted` 也可能有非零 `certificateFileCleanupFailures`，表示证件文件需后续补偿清理。`isCanceled` 表示未完成整轮；连接中断时也可能拿不到结果，不能据此推断没有删除任何账号。

列表没有分页或批次上限，执行请求会等待清理结果，不返回后台任务 ID，也没有进度查询接口。界面应展示执行中状态并避免重复提交；失败或断线后重新预览当前状态。

申请超时拒绝和 LLM 审计清理目前仅有自动策略配置，没有手工预览/执行 HTTP 端点；孤儿文件清理没有独立 HTTP 端点。内部入口和事务边界见[账号清理实现](../account-cleanup.md)与[维护任务实现](../maintenance-cleanup.md)。
