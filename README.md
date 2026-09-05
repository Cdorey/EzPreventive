# EzNutrition

面向公共卫生与营养专业人员的开源营养评估与咨询工作台。

EzNutrition 基于 Blazor WebAssembly、WPF Blazor Hybrid 与 ASP.NET Core，提供多咨询对象工作区、能量与膳食营养素评估、24 小时膳食回顾、SOAP 信息录入以及 AI 辅助膳食建议。主要营养计算和咨询状态在浏览器或桌面进程中完成；服务端负责身份认证、参考数据访问、AI 调用及其审计。

> EzNutrition 的输出仅用于辅助专业判断，不替代诊断、治疗或个体化医学建议。AI 生成内容可能存在遗漏或错误，必须由具备相应资质和知识的人员复核。

## 核心能力

- **咨询工作区**：支持创建和切换多个咨询对象，集中管理评估、膳食调查、SOAP 信息和建议生成流程。
- **营养评估**：提供能量需求、膳食营养素参考摄入量等计算与速查能力。
- **膳食调查**：支持 24 小时膳食回顾及相关膳食结构分析。
- **AI 辅助建议**：通过服务端调用生成式 AI，并以流式方式呈现推理和建议；供应商能力由可替换适配器隔离，支持取消、失败反馈与重新发送。
- **身份与权限**：提供邮箱确认与重发、密码修改与邮箱找回、邮箱/手机号码修改、专业身份相关流程和受权限保护的功能入口。
- **机构服务连接**：WPF 可连接机构自行部署的兼容后端，默认执行严格 HTTPS 验证；用户主动确认风险后也可使用自签名 HTTPS 或不加密 HTTP。
- **登录续期**：短期 JWT 配合一次性刷新凭据，访问临期自动续期；WPF 用端点级 DPAPI 保存刷新凭据，浏览器使用 HttpOnly Cookie。接口协议见 [HTTP API](./docs/http-api/authentication.md)，内部机制与部署见[认证会话说明](./docs/authentication-sessions.md)。
- **本机档案**：提供格式无关的档案模型、校验与工作流；WASM 使用 IndexedDB，WPF 使用当前用户的应用数据目录，并支持 XML 文档导入、另存为及资源管理器定位。
- **开放实现**：营养领域逻辑、应用编排和宿主适配已分层，便于测试、复核并复用于未来的其他宿主。

## 数据与隐私边界

- 多数营养计算在当前客户端执行，以减少不必要的数据上传；身份认证、参考数据查询和 AI 建议仍需要访问服务端。
- 当前咨询工作区以客户端会话为运行边界；完成咨询后应主动保存到本机档案库或导出 XML 文档。浏览器档案受站点数据保留策略影响；WPF 档案默认位于 `%LOCALAPPDATA%\EzSuit\EzNutrition\Archives`，不会默认进入可能由 OneDrive 同步的文档目录。两者都不能替代医疗机构正式档案系统的备份、审计与保留制度。
- WPF 的 XML 与调阅索引未做静态加密。文件名不包含患者姓名，但索引和 XML 正文仍可能包含敏感信息；应依赖受控 Windows 账户、磁盘保护和机构策略，并谨慎配置任何自定义或同步目录。
- WPF 保存的刷新凭据与档案分离并由 DPAPI 当前用户范围加密，不保存密码；这不能抵御已取得该 Windows 用户执行权限的恶意程序。显式退出会撤销当前会话并清除当前连接保存的副本，离线时服务端撤销无法立即完成。
- 使用 AI 建议功能时，请求会被发送至服务端及其配置的外部模型服务。服务端会保存登录用户标识、完整请求、模型返回的推理与建议内容以及处理时间，用于安全审计和防止接口滥用。
- AI 请求可能包含咨询对象信息、膳食回顾和临床信息。请只提交完成任务所必需的数据，避免输入不必要的姓名、证件号码、联系方式等直接身份标识，并遵守适用的数据保护和医疗信息管理要求。

## 项目结构

| 项目 | 主要职责 |
| --- | --- |
| `Libraries/EzNutrition.Domain` | 营养领域模型、状态与纯计算规则 |
| `Libraries/EzNutrition.Application` | 咨询用例编排、应用服务及外部能力端口 |
| `Libraries/EzNutrition.UI` | 传输无关、可独立渲染测试的 Razor 营养组件 |
| `Libraries/EzNutrition.Presentation` | 多客户端共享的 App、页面、布局、会话、HTTP/SSE 适配与静态资源 |
| `Libraries/EzNutrition.Archives.Contracts` | 格式无关的档案模型、校验、编解码与仓储契约 |
| `Libraries/EzNutrition.Archives.Xml` | 仅依赖档案契约的版本化 XML codec、安全读取与未知内容往返保留 |
| `Hosts/EzNutrition.Client` | Blazor WebAssembly 启动与组合根、IndexedDB/浏览器文件适配和浏览器入口资源 |
| `Hosts/EzNutrition.Wpf` | WPF Blazor Hybrid 组合根、文件系统档案、用户连接设置、DPAPI、桌面证书策略、Windows 文档交互与 Shell 集成 |
| `EzNutrition.Server` | ASP.NET Core API、认证授权、参考数据访问、AI 调用与审计 |
| `Libraries/EzNutrition.AiAgency` | 生成式 AI 供应商适配 |
| `Libraries/EzNutrition.Shared` | 客户端与服务端共享的传输 DTO、参考数据实体和授权策略 |
| `Tests/*.Tests` | Application、Archives、Client、WPF 和 Server 的行为、安全流程与架构边界测试 |

依赖关系遵循“领域与应用层不感知具体宿主”的方向：Application 通过端口描述所需能力，WASM、WPF 或其他宿主在各自组合根中提供具体实现。WASM 与 WPF 是互不引用的并列宿主，共享完整工作台时统一依赖 `EzNutrition.Presentation`。详细边界和各上级类库盘点见[项目与依赖边界](./docs/project-architecture.md)。

开发文档入口见[文档目录](./docs/README.md)；前后端对接以 [HTTP API 2.2](./docs/http-api/README.md) 为准。

## 本地开发与验证

仓库以 [EzPreventive.sln](./EzPreventive.sln) 作为唯一解决方案入口，需要 .NET 10 SDK。
当前产品版本集中声明在 [Directory.Build.props](./Directory.Build.props)，四段版本的升级和兼容规则见[版本管理](./docs/versioning.md)。

```powershell
dotnet restore .\EzPreventive.sln
dotnet build .\EzPreventive.sln -c Release --no-restore
dotnet test .\EzPreventive.sln -c Release --no-build --no-restore
```

Windows 上可直接运行 Hybrid 宿主：

```powershell
dotnet run --project .\Hosts\EzNutrition.Wpf\EzNutrition.Wpf.csproj
```

WPF 默认以严格 HTTPS 连接 `https://eznutrition.cdorey.net/`，服务端、安全策略与档案目录均可配置。路径选择、风险边界、免登录行为与发布命令见 [WPF Hybrid 宿主说明](./docs/wpf-hybrid-host.md)。

运行服务端还需要配置两个 SQL Server 连接、JWT、邮件服务和 AI 供应商凭据。首次执行 `AuthInitialize` 创建管理员时，还必须通过受控配置提供 `AuthBootstrap:AdminPassword`（环境变量形式为 `AuthBootstrap__AdminPassword`）。请使用用户机密、环境变量或受控配置源，不要把真实凭据提交到仓库或日志。

生产部署还应按实际拓扑配置[受信任的反向代理](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0)，确保登录和账户恢复的 IP 限流取得真实客户端地址；容器或多实例部署则应[持久化并共享 Data Protection 密钥环](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0)，避免邮箱确认和密码重置令牌因重启或实例切换而失效。

数据库迁移默认不会在服务启动时自动执行。部署时应把结构迁移作为显式步骤，并在启动应用前确认两个数据库均无待应用迁移。

## 近期重要变更

- **2026-08-24 — WPF 机构连接与安全免登录**：增加原生连接设置窗口、默认严格 HTTPS、自签名/HTTP 显式风险模式、持续安全警示、端点级 DPAPI 登录信息和重启自动登录；登录流程的共享端口仍位于 Presentation，具体保护与证书策略只属于 WPF。
- **2026-08-23 — WPF Blazor Hybrid 本地宿主**：以独立宿主引用共享 Presentation RCL，加入 Windows 文件系统档案、打开/另存为对话框、导出后资源管理器定位、本机目录入口及发布期 WebView2 数据目录；WPF 不引用 WASM，Domain 和服务端计算逻辑保持不变。
- **2026-08-14 — 本机 XML 档案闭环**：建立格式无关的 Application 档案用例，加入独立 XML codec、浏览器 IndexedDB/文件适配器、桌面优先的保存与只读调阅界面，并保持 UI、格式和宿主存储之间的单向依赖。
- **2026-08-08 至 09 — 可复用架构分层**：拆分 Domain、Application 和 UI，升级至 .NET 10，并以 `IAiAdviceGateway` 隔离 AI 应用流程与浏览器 HTTP/SSE 传输；补充应用、适配器和架构边界测试。详见 [PR #2](https://github.com/Cdorey/EzPreventive/pull/2)。
- **2026-08-09 — 仓库聚焦 EzNutrition**：将 EzAttached 迁移至 [独立仓库](https://github.com/Cdorey/EzAttached)，删除废弃的 DataInserter 临时工具和冗余解决方案筛选文件。详见 [PR #3](https://github.com/Cdorey/EzPreventive/pull/3) 和 [PR #4](https://github.com/Cdorey/EzPreventive/pull/4)。
- **2026-08-08 — 档案契约基线**：建立格式无关的档案资源、身份、引用、校验、编解码和仓储边界，并接入运行时咨询快照；具体 XML 实现尚未加入。详见 [PR #2](https://github.com/Cdorey/EzPreventive/pull/2)。
- **2026-08-07 — 工作流加固**：完成响应式营养工作台重构，加固认证、DRIs、SOAP 信息录入和 AI 流式生成流程，使生成过程可取消、失败可感知。详见 [PR #1](https://github.com/Cdorey/EzPreventive/pull/1)。
- **2025-02 至 2025-03 — 核心功能形成**：陆续完成 24 小时膳食回顾、多咨询对象切换、用户中心、SOAP 信息录入及 AI 辅助膳食建议等基础能力。

## 下一阶段开发计划

以下顺序表示当前优先方向，不代表固定发布日期。

1. **P0：建立可重复的工程与数据基线**
   - 为 Pull Request 增加 .NET 10 的还原、Release 构建和测试自动检查。
   - 将数据库结构迁移、营养参考数据初始化和身份系统初始化拆成明确步骤。
   - 设计版本化、幂等、可审计的参考数据初始化器，并在分发数据前核对数据来源与授权；同步验证幂等、事务失败和版本识别行为。
   - 明确 AI 审计数据的最小字段、访问权限、脱敏方式和保留策略，并为策略调整补充针对性测试。

2. **P1：加固本机档案与继续编辑流程**
   - 在更多浏览器和 WPF 发布配置中验证 IndexedDB、文件导入导出和大文档取消行为，并明确本机档案保留、迁移与清理提示。
   - 完成从 `ArchiveDocument` 恢复咨询工作区的受控反向映射，在保留资源身份、并发上下文和未知源内容的前提下提供“继续编辑”。
   - 根据互操作需求发布 XML 格式说明与兼容样本，并为后续格式迁移保留确定性验证集。
   - 评估导出文档的加密容器、签名和机构审计需求；纯 XML 默认不承担静态加密职责。

3. **P1：继续收窄展示层耦合**
   - 逐步将 Presentation 页面中已经形成稳定语义的流程下沉为 Application 用例，减少页面对 Domain、Archives.Contracts 的非必要直接操作。
   - 持续用架构测试保护 UI、Presentation 与并列宿主边界，避免把宿主通用代码重新放回任一可执行宿主。
   - 为 `EzNutrition.Shared` 的历史性宽职责制定独立迁移方案后再讨论拆分，避免在 WPF 或日常功能迭代中顺带改动服务端协议。

4. **P2：补齐集成验证与部署手册**
   - 增加服务端鉴权、AI 审计、控制器和数据库集成测试。
   - 增加数据库可达性、迁移状态和参考数据版本的健康检查。
   - 补充本地配置、部署、回滚、备份和首次初始化文档，再根据明确的部署目标评估持续交付方案。

## 适用范围与免责声明

本项目主要面向公共卫生执业医师、临床医师、营养专业技术人员，以及公共卫生和营养相关专业的高校师生。普通用户不应将计算或 AI 输出直接用于自我诊断、治疗或改变既有医疗方案。

计算规则参考营养学教材、膳食指南、膳食营养素参考摄入量和食物成分资料。使用者应核对具体功能所采用的资料版本，并结合当地规范、服务对象情况和自身专业判断解释结果。

## 许可证与联系

本项目采用 [GNU Affero General Public License v3.0](./LICENSE.txt) 开源。使用、修改和再发布时，请以许可证原文为准。

- 源代码：[Cdorey/EzPreventive](https://github.com/Cdorey/EzPreventive)
- 项目站点：[eznutrition.cdorey.net](https://eznutrition.cdorey.net/)
- 联系邮箱：[bsense@live.com](mailto:bsense@live.com)
