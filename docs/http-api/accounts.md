# 账号与认证申请

[返回 HTTP 文档目录](./README.md)。下面列出字段均使用 JSON 名称；“必填”字符串不可为空。登录协议见[认证会话](./authentication.md)。

## 匿名账号接口

| 方法与路径 | 请求 | 成功结果 |
| --- | --- | --- |
| `POST /Auth/Register` | `userName`、`password`、`email` 必填；`phoneNumber`、`professionalIdentity` 可选 | 200 `{success,message,uploadTicket}` |
| `GET /Auth/ConfirmEmail` | 查询参数 `userId`、`token` 必填 | 302 跳转 `/account/confirm-email` 并携带参数；此 GET 不确认邮箱 |
| `POST /Auth/ConfirmEmail` | `userId`、`token` | 200 操作结果 |
| `POST /Auth/ResendEmailConfirmation` | `email` | 202 操作结果 |
| `POST /Auth/ForgotPassword` | `email` | 202 操作结果 |
| `POST /Auth/ResetPassword` | `userId`、`token`、`newPassword`、`confirmPassword` | 200 操作结果 |
| `POST /Auth/ConfirmEmailChange` | `userId`、`newEmail`、`token` | 200 操作结果 |
| `POST /Auth/UploadCertificate/{uploadTicket}` | multipart 文件字段 `certificateFile` | 200 空正文 |

操作结果为 `{ "success": true, "message": "..." }`；确认/重置等业务失败为 400 同形对象。注册业务失败可能为 400 文本；字段验证使用框架 400。注册不会直接登录，也不分配默认角色。

`professionalIdentity` 为 `{identityType,institutionName}`，两项必填，例如 `Physician` 和机构名；不提交则 `uploadTicket` 可为空。邮箱须格式有效、用户名唯一、注册密码至少 6 字符；当前不强制大小写、数字或特殊字符组合。

恢复类 DTO 的 `userId` 最多 450 字符，`token` 最多 4096，邮箱最多 256。新密码 6–256 字符，确认密码必填且相等。确认令牌应原样使用邮件链接提供的值，不自行解码再编码。

发信接口的 202 是统一受理结果，不证明账号存在或邮件必然发送；邮件任务无法受理时返回 503 `{success:false,message}` 和 `Retry-After: 60`。限流仍可能返回 429。

上传票据来自注册或提交认证申请。票据必须仍关联待审核申请；格式错误、无效票据、空文件或不支持的图片为 400。仅接受 JPEG/PNG，检查扩展名、媒体类型和文件内容；请求体上限 50 MiB（包含 multipart 开销），超限可能返回 413。此端点不要求 Bearer，应把票据当作上传凭据。

## 当前用户接口

以下均要求已登录，操作对象由当前身份决定。

| 方法与路径 | 请求 | 成功结果 |
| --- | --- | --- |
| `GET /User/Profile` | 无 | 200 用户详情 |
| `POST /User/ChangePassword` | `currentPassword`、`newPassword`、`confirmPassword` | 200 操作结果 |
| `POST /User/RequestEmailChange` | `currentPassword`、`newEmail` | 200 操作结果；原邮箱保留至确认成功 |
| `PUT /User/PhoneNumber` | `currentPassword`、可空 `phoneNumber` | 200 操作结果；可清空手机号 |
| `POST /User/CreateProfessionalIdentity` | `{identityType,institutionName}` | 200 `{success,message,uploadTicket}` |
| `GET /User/ProfessionalIdentity` | 无 | 200 申请数组；没有申请时 404 |

当前密码必填、最多 256 字符；新密码约束同上；新邮箱必填、格式有效、最多 256；手机号最多 64 字符且满足电话格式。安全操作业务失败返回 400 操作结果。修改密码、确认换邮箱或修改手机号后旧凭据会失效，应重新建立会话；手机号变更后确认状态重置。

用户详情：`userId:string`、`userName:string`、`email:string`、`emailConfirmed:boolean`、`phoneNumber:string`、`phoneNumberConfirmed:boolean`、`roles:string[]`、`claims:{type:string,value:string}[]`。未配置的邮箱/手机号在详情中为空字符串。

## 申请对象

用户及管理员申请列表共用此结构：

| 字段 | 类型/意义 |
| --- | --- |
| `id`、`version` | GUID；version 用于审批并发协调 |
| `userId` | string，账号标识 |
| `identityType`、`institutionName` | string，申请类型、机构 |
| `requestTime` | UTC 提交时间 |
| `status` | 整数：0 Pending、1 Approved、2 Rejected |
| `processedTime` | 可空 UTC 处理时间 |
| `processDetails`、`remarks` | 可空 string，处理意见和备注 |
| `certificateTicket` | 可空 GUID，证件标识 |

申请审核通过与账号拥有角色是不同事实，审批端点不会自动授予角色。管理员修改契约见[站点管理](./administration.md)。
