# 站点管理

[返回 HTTP API 目录](./README.md)。本页所有接口要求管理员角色 `Admin`。

## 用户与角色

| 方法与路径 | 输入 | 成功响应 |
| --- | --- | --- |
| `GET /Admin/Users/{role?}` | 可省略角色路径段；提供时按角色筛选 | 200，用户数组，未分页 |
| `GET /Admin/GetUserInfo/{userId}` | 用户 ID | 200，完整用户资料 |
| `PUT /Admin/UpdateUser` | 下述完整用户资料 JSON | 200，`{message}` |
| `DELETE /Admin/DeleteUser/{userId}` | 用户 ID | 200，删除结果 |
| `GET /Admin/Roles` | 无 | 200，角色名称字符串数组 |
| `POST /Admin/AddRole` | **表单**字段 `newRole` | 200，`{message}` |
| `GET /Admin/RoleClaims/{roleName}` | 角色名称 | 200，`{roleName,claims:[{type,value}]}` |
| `PUT /Admin/UpdateRoleClaims/{roleName}` | JSON 数组 `[{"type":"Permission","value":"Prescription"}]` | 200，`{message}` |

用户列表项包含 `id`、`userName`、`normalizedUserName`、`email`、`emailConfirmed`、`phoneNumber`、`phoneNumberConfirmed`、`twoFactorEnabled`、`lockoutEnabled`、`accessFailedCount`，不包含角色或声明。邮箱和手机号可以为空。

完整用户资料结构与[个人资料](./accounts.md)一致：`userId`、`userName`、`email`、`emailConfirmed`、`phoneNumber`、`phoneNumberConfirmed`、`roles`、`claims`。更新时提交完整资料，用户名、邮箱和用户 ID 不可为空；`roles` 与 `claims` 是完整替换，空数组会清空。角色必须存在；声明的 `type`、`value` 不可为空，也不能设置系统保留的身份、会话声明。确认标记不是可直接授予的权限：更换邮箱或手机号会清除对应确认状态。保存用户资料会使其旧会话失效。

角色声明更新同样是完整替换，`[]` 清空全部声明；不能提交 `{claims:[...]}` 信封。更新会使该角色用户的旧会话失效。角色创建会去除名称首尾空白，重名返回 409 文本。用户或角色不存在通常返回 404；验证失败为 400 文本、Identity 错误数组或框架验证响应，参见[通用错误约定](./README.md#错误与重试)。

单用户删除结果包含 `message`、`deletedAiAudits`、`deletedCertificationRequests`、`certificateFileCleanupAttempts`、`certificateFileCleanupFailures`。数据库删除成功后，即使证件文件清理失败仍返回成功；文件失败数需单独展示。用户不存在返回 404，Identity 拒绝删除返回 400 错误数组。

## 发布公告与政策文本

`PUT /Admin/Notification` 接收：

```json
{
  "noticeTitle": "维护通知",
  "noticeDescription": "公告正文",
  "kind": 0
}
```

`noticeDescription` 必填，标题可省略，`kind` 默认 0。取值为 0 登录后公告、1 登录前介绍、2 用户协议、3 隐私政策。每次提交新增一条内容，读取接口选择该类型最新内容，并非按 ID 修改历史记录。成功为 200 空正文，读取契约见[公共信息](./system-info.md)。

## 认证申请审核

| 方法与路径 | 输入 | 成功响应 |
| --- | --- | --- |
| `GET /Admin/ProfessionalCertificationRequests` | 无 | 200，[认证申请数组](./accounts.md#申请对象)，无记录为 `[]` |
| `GET /Admin/CertificateImage/{ticket}` | 证件票据 GUID | 200，图片二进制及其 Content-Type |
| `PUT /Admin/UpdateRequest` | 认证申请 JSON，见下文 | 200，`{message,version}` |

图片票据格式不正确返回 400，图片不存在返回 404。已处理申请的图片可能已经删除，不能假定有票据就一定可下载。

更新申请时发送列表中的完整申请对象，并修改 `status`、`processDetails`、`remarks`。必须保留非空 `id`、`version`，以及当前请求模型要求的 `userId`、`identityType`、`institutionName`。服务端只使用 ID、版本、状态、处理意见和备注完成更新；提交的申请人、申请时间、处理时间或票据不会据此被修改。

- `status = 0`（待审核）：仅允许编辑仍待审核且版本一致的申请，不能重新打开已结束申请。
- `status = 1`（批准）或 `2`（拒绝）：允许覆盖已有审核结果；即使读取后版本变化，也会按本次决定尝试保存。因此版本不是人工决定的严格“旧版本必拒绝”条件。
- 成功返回新的 `version`；缺少有效版本或状态无效返回 400，记录不存在返回 404，无法完成并发更新返回 409：`{"code":"certification_changed","message":"..."}`。404/409 后应重新获取清单。

**保存批准结果不会授予角色或声明，保存拒绝也不会撤销权限。** 若业务流程需要两者，调用方分别使用用户更新与申请更新接口；两次请求不具备共同的原子性，第二步失败不会撤回第一步的权限变更。重新批准不会恢复此前删除的证件图片，接口也不提供审核历史。

服务端并发控制及当前管理页面的编排见[审核实现说明](../certification-review.md)。
