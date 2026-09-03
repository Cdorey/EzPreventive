# 认证会话（2.2）

登录签发短期访问 JWT 和长期的一次性刷新凭据。JWT 携带 `sid`、`jti`、用户身份及会话建立时的安全戳指纹；刷新凭据使用 32 字节密码学随机数，数据库仅保存其 SHA-256 摘要。

本项目采用每次刷新都轮换凭据、发现已消费凭据重用便撤销所属会话的策略，参考 [RFC 9700 §4.14 的刷新令牌保护建议](https://www.rfc-editor.org/rfc/rfc9700.html#section-4.14)。这是本项目的第一方登录协议，未实现 OAuth/OIDC 授权服务器。

## 期限与轮换

| 项目 | 默认值 | 配置或行为 |
| --- | --- | --- |
| 访问令牌 | 15 分钟 | `JwtSettings:AccessTokenMinutes`，允许 2–60 分钟 |
| 刷新空闲期限 | 7 天 | `JwtSettings:RefreshIdleDays`，每次成功刷新重新计算 |
| 会话绝对期限 | 30 天 | `JwtSettings:SessionLifetimeDays`，自登录起固定，不随刷新延长 |
| 提前刷新 | 剩余不足 1 分钟 | 客户端在发出业务请求前按需执行，不设常驻刷新定时器 |
| JWT 时钟容差 | 30 秒 | 服务端校验；期限统一使用 UTC |

空闲期限不得超过绝对期限，两项均允许 1–90 天。`RememberLogin` 仅控制宿主是否跨重启保留凭据，不延长服务端期限。临近绝对到期时，访问令牌和刷新空闲期限都截断到绝对期限。

`AuthenticationSessions` 记录会话归属、浏览器/桌面模式、固定安全戳、期限及撤销状态；`RefreshTokens` 记录摘要与消费时间。会话版本条件更新、消费旧凭据和写入新摘要位于同一数据库事务，多服务实例也只有一个请求能成功消费同一凭据。重复使用已消费凭据会撤销整个会话，包括它已经换出的 JWT。已消费记录保留到会话到期，由后台任务每天清理过期会话及关联记录。

每个受保护请求都校验 JWT、当前账号安全戳、锁定状态及会话是否有效。普通注销只撤销当前会话；改密、邮箱/手机号变更和现有权限管理操作沿用安全戳机制，使该账号全部旧会话失效；账号删除级联删除会话及刷新记录。

## HTTP 契约

以下路径均相对于应用部署基路径，认证响应设置 `Cache-Control: no-store`。业务请求仍使用 `Authorization: Bearer <accessToken>`。

| 接口 | 请求 | 响应 |
| --- | --- | --- |
| `POST Auth/Login` | JSON：`userName`、`password`、`rememberLogin` | 桌面令牌对象，包含 `refreshToken` |
| `POST Auth/Refresh` | JSON：`refreshToken`、`sessionId` | 新访问令牌与新刷新凭据 |
| `POST Auth/Logout` | JSON：`refreshToken`、`sessionId` | `204`；重复注销幂等 |
| `GET Auth/Browser/Csrf` | 同源 Cookie | `{ "requestToken": "..." }`，同时设置防伪 Cookie |
| `POST Auth/Browser/Login` | 登录 JSON、防伪 Cookie、`X-CSRF-TOKEN` | 访问令牌对象，另设刷新 Cookie |
| `POST Auth/Browser/Refresh` | JSON：`sessionId`，同源 Cookie、CSRF | 新访问令牌并轮换 Cookie；无 Cookie 返回 `204` |
| `POST Auth/Browser/Logout` | JSON：`sessionId`，同源 Cookie、CSRF | 撤销会话、删除 Cookie，返回 `204` |

令牌对象字段为 `sessionId`、`accessToken`、`accessTokenExpiresAtUtc`、`refreshExpiresAtUtc`、`sessionExpiresAtUtc`、`rememberLogin`。只有桌面接口包含 `refreshToken`。恢复浏览器 Cookie 时允许省略 `sessionId`，后续刷新、注销由客户端携带预期值，避免旧窗口操作新账号。

错误正文使用 `{ "code": "...", "message": "..." }`：

| 状态 | `code` | 客户端行为 |
| --- | --- | --- |
| `401` | `invalid_credentials` | 登录失败，保留手动登录界面 |
| `401` | `access_token_expired` | 对可重发请求刷新并最多重试一次 |
| `401` | `session_invalid` | 当前会话失效，清除本地身份并要求重新登录 |
| `409` | `session_changed` | 其他窗口已经切换会话，禁止以新账号重发旧请求 |
| `403` | 不用于续期 | 保留登录状态，按权限错误处理 |
| `429`、`5xx` 或网络失败 | 不视为永久失效 | 保留恢复凭据，等待用户重试 |

框架生成的 CSRF/请求验证失败可返回其原有 `400` 响应。认证模块将不可识别的响应作为失败处理，不自动重发一次性刷新请求。登录和刷新分别使用已有的登录限流和新增的每 IP 每分钟 120 次刷新限流。

## 客户端边界

`UserSessionService` 只管理身份、短期令牌和会话状态，通过 `IAuthenticationSessionClient` 调用宿主。并发请求通过同一闸门共享刷新结果；单个请求取消只取消自己的等待。登录、退出或跨窗口变更会推进本地状态版本，迟到的旧响应不能恢复旧会话。

`CustomAuthorizationMessageHandler` 只为配置端点附加令牌，遇到显式认证头保持调用方行为。认证失败后的自动重试限于无流式请求体的 GET/HEAD，以及调用方明确标记、使用可重复读取内存内容的请求。AI/SSE 只允许在服务端认证拒绝、业务尚未开始时重试一次；开始接收流后不自动重启生成。

临时刷新失败时，仍有效的访问令牌可继续使用，后续刷新尝试间隔至少 5 秒；访问令牌过期后阻止业务请求发出，但不因暂时断网清除界面身份或工作区。

WPF 通过独立的 `Authentication` HTTP 客户端交换 JSON，禁用 Cookie 和自动重定向。勾选保持登录时用 DPAPI `CurrentUser` 加密会话标识、刷新凭据和绝对期限，按“端点 + 传输安全策略”隔离。排他文件锁覆盖读取、网络轮换及原子保存；不勾选时仅保留进程内凭据。详情见 [WPF 宿主说明](./wpf-hybrid-host.md#记住登录)。

浏览器的 `EzNutrition.Refresh` Cookie 为 HttpOnly、SameSite=Strict，生产环境始终 Secure，路径限制为部署基路径下的 `/Auth/Browser`。勾选保持登录才设置持久化到期时间；未勾选使用会话 Cookie，其生命周期也受浏览器会话恢复设置影响。浏览器 POST 经 ASP.NET Core 防伪校验，参见 [Microsoft 的防伪请求说明](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0)。访问 JWT 仅在内存中；Local Storage 只存非秘密的会话标识、变更通知与待完成的退出意图。

浏览器依赖同源部署、HTTPS 安全上下文和 Web Locks，跨标签页串行登录、轮换与注销。其他标签页登录或退出会通知当前页面重新恢复共享会话；不支持协调能力时明确报错。离线退出会先保存待注销标记，重载时优先完成注销，防止残留 Cookie 自动恢复登录。

## 升级、部署与验证

1. 前后端统一发布 `2.2.0.0`。旧 `Auth/Login` 表单/纯字符串响应契约不再兼容；旧 JWT 缺少 `sid`，升级后要求重新登录。
2. 发布前对 **ApplicationDbContext** 应用迁移 `20260903000902_AddAuthenticationSessions`。它只新增认证表及索引，不修改营养参考数据库。可按部署流程生成审阅 SQL：

   ```powershell
   Push-Location EzNutrition.Server
   dotnet tool restore
   dotnet ef migrations script --idempotent --context ApplicationDbContext --output authentication-migrations.sql
   Pop-Location
   ```

   使用部署环境已有的受保护配置提供连接字符串和 JWT 密钥，审阅后按现行数据库发布流程应用。也可沿用 `DatabaseStartup:ApplyMigrationsOnStartup` 的受控迁移入口。
3. WPF 首次读取旧版密码凭据文件时直接删除，用户重新登录一次。浏览器无历史刷新 Cookie，首次也需要登录。
4. 多实例共享 ApplicationDb、JWT 签名配置及 Data Protection 密钥环；反向代理保持正确的 HTTPS、基路径和可信客户端地址信息。认证响应及 Cookie 不得被代理缓存。

本次实现不自动修改开发或生产数据库。回滚前后端必须协调；新表可保留，回滚后旧版程序的登录策略以旧版实现为准。

一次性轮换采用严格重放检测，不设置旧凭据宽限期。服务端已经消费刷新凭据但响应丢失、或客户端无法安全保存新凭据时，可能需要重新登录。桌面离线注销会清除本地副本，但服务端会话只能等后续撤销或期限届满；浏览器保留待注销意图，恢复网络后在下一次认证操作中完成。

从仓库根目录执行回归验证：

```powershell
dotnet test EzPreventive.sln -c Release
node --test Tests/EzNutrition.Client.Tests/Browser/auth-session.test.mjs
Push-Location EzNutrition.Server
dotnet tool restore
dotnet ef migrations has-pending-model-changes --context ApplicationDbContext
Pop-Location
```

测试覆盖数据库轮换竞争、重放与安全戳失效，真实 MVC/JWT/CSRF HTTP 链路，共享请求的单次刷新与重试、注销竞争，DPAPI 格式升级与多实例协调，以及浏览器互操作和多标签页脚本。SQL Server 部署迁移仍须按目标环境执行验收。
