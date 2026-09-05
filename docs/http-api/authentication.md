# 认证会话

[返回 HTTP 文档目录](./README.md)。所有本页端点不要求现有 Bearer 令牌；认证响应禁止缓存。业务端点仍使用 Bearer，不以刷新 Cookie 直接授权。

## 端点

| 方法与路径 | 请求 | 成功结果 |
| --- | --- | --- |
| `POST /Auth/Login` | 登录 JSON | 200 桌面令牌对象 |
| `POST /Auth/Refresh` | 刷新 JSON | 200 新令牌对象 |
| `POST /Auth/Logout` | 刷新 JSON | 204 |
| `GET /Auth/Browser/Csrf` | 无正文 | 200 `{requestToken:string}`，同时设置防伪 Cookie |
| `POST /Auth/Browser/Login` | 登录 JSON、CSRF | 200 浏览器令牌对象并设置刷新 Cookie |
| `POST /Auth/Browser/Refresh` | 刷新 JSON、Cookie、CSRF | 200 新令牌并轮换 Cookie；没有刷新 Cookie 时 204 |
| `POST /Auth/Browser/Logout` | 刷新 JSON、Cookie、CSRF | 204 并清除刷新 Cookie |

登录 JSON：

```json
{"userName":"example","password":"example-password","rememberLogin":true}
```

`userName` 必填，最多 256 字符；`password` 必填，最多 4096 字符；`rememberLogin` 默认 false，只控制跨重启恢复，不改变服务端会话期限。登录要求账号状态有效，配置了邮箱的账号还须完成邮箱确认。

刷新 JSON：

```json
{"sessionId":"d1f4f03e-c911-4486-8932-6fb294bc35ca","refreshToken":"opaque-refresh-secret"}
```

桌面刷新必须提供有效 `refreshToken`（最多 128 字符）；退出对不存在的凭据幂等。`sessionId` 是可空 GUID，用于约束目标会话。浏览器不提交刷新秘密，使用 `{ "sessionId": "..." }`；首次恢复尚不知道会话时发送 `{}`。后续刷新与退出应携带原会话标识，防止操作其他窗口的新账号。

## 成功响应

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `sessionId` | GUID | 本次会话身份，刷新后保持不变 |
| `accessToken` | string | 业务调用所需的 JWT |
| `accessTokenExpiresAtUtc` | UTC 时间 | 本次访问令牌到期时间 |
| `refreshExpiresAtUtc` | UTC 时间 | 本次响应确认的空闲期限；其他窗口刷新后可能延长 |
| `sessionExpiresAtUtc` | UTC 时间 | 固定的会话绝对到期时间 |
| `rememberLogin` | boolean | 是否请求跨重启保留登录 |
| `refreshToken` | string | 仅桌面响应包含；浏览器省略该字段 |

默认访问令牌 15 分钟、刷新空闲期限 7 天、绝对期限 30 天，部署可调整，以响应为准。成功刷新更新访问令牌和一次性刷新凭据，并延长空闲期限，但不超过绝对期限。使用已消费的刷新凭据会使所属会话失效；浏览器与桌面凭据不能混用。

普通业务请求不会在响应中顺带附送新令牌，调用方需要显式调用刷新接口。退出撤销当前会话，已有访问令牌也不再可用。密码或相关账号安全信息改变后可能要求所有旧会话重新登录。

## 浏览器交换

先 GET CSRF，保存响应中的 `requestToken`，再在上述三个 POST 中发送 `X-CSRF-TOKEN`，并让浏览器携带同源 Cookie。缺少有效防伪 Cookie 或请求令牌时返回 400，正文不保证是业务错误对象。

刷新 Cookie 名为 `EzNutrition.Refresh`，HttpOnly、SameSite=Strict，Path 为部署基路径加 `/Auth/Browser`；生产环境 Secure，开发 HTTP 可例外。勾选保持登录才设置持久化到期时间，未勾选为会话 Cookie。浏览器登录成功会替换原刷新 Cookie 并撤销其原会话。

## 错误

```json
{"code":"session_changed","message":"登录会话已发生变化。"}
```

`message` 为示例，不应据其文字判断分支。

| 状态 | code | 含义 |
| --- | --- | --- |
| 401 | `invalid_credentials` | 账号、密码、邮箱确认或锁定状态不允许登录 |
| 401 | `access_token_expired` | 受保护业务请求的访问令牌过期，可尝试刷新 |
| 401 | `session_invalid` | 会话失效或认证凭据无效，需要重新登录 |
| 409 | `session_changed` | 凭据所属会话与预期不一致，不得换成新账号重放旧操作 |

JWT 验证有 30 秒时钟容差。401 挑战带 `WWW-Authenticate: Bearer` 和禁止缓存头。其他验证、限流及服务错误遵循[通用约定](./README.md)。临时失败与永久失效必须区分；一次性轮换已在服务端成功但响应丢失时，可能需要重新登录。

持久化、跨窗口锁、自动重试与部署属于[认证会话实现](../authentication-sessions.md)。
