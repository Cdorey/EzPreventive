# 项目与依赖边界

本文记录 EzNutrition 当前各项目的职责和允许依赖，避免把“可被多个项目使用”误解为“可以放进任意名为 Shared 的项目”。箭头 `A → B` 表示 A 可以引用 B。

## 总体原则

- WASM、WPF 和后续可执行客户端是并列宿主，彼此不能建立项目或程序集引用。
- 真正跨宿主一致的代码进入具有明确职责的类库；平台存储、凭据保护、证书策略、文件交互、启动生命周期和入口资源留在各自宿主。
- Domain 和 Application 不感知 WPF、WASM、HTTP、WebView、文件对话框或 IndexedDB。
- `EzNutrition.UI` 保持传输无关；完整路由工作台和两个客户端共同使用的远程服务适配由 `EzNutrition.Presentation` 承担。
- `EzNutrition/Shared` 是历史形成的客户端/服务端协议与参考数据共享项目，不是通用代码收容箱。

## 当前主依赖图

```text
EzNutrition.Client (WASM) ─┐
                           ├──> EzNutrition.Presentation ──> EzNutrition.UI
EzNutrition.Wpf ───────────┘                │                        │
        │                                   └────────────┬───────────┘
        ├──> EzNutrition.Archives.Xml                    v
        └──> 宿主档案适配器                    EzNutrition.Application
                                                    │      │      │
EzNutrition.Archives.Xml ──> Archives.Contracts <──┘      │      │
                                                           v      v
                                                EzNutrition.Domain  EzNutrition.Shared

EzNutrition.Server ──> EzNutrition.AiAgency ──> EzNutrition.Shared
        └────────────> EzNutrition.Client（仅托管 WASM 发布资源）
```

Client 与 WPF 还会在各自组合根中直接引用 Application、Archives.Contracts 和 Archives.Xml，以提供具有不同来源标识和平台语义的档案实现。它们不通过 Presentation 隐藏这些宿主决策。

## 项目职责清单

| 项目 | 应承担 | 不应承担 |
| --- | --- | --- |
| `EzNutrition.Archives.Contracts` | 格式无关档案模型、标识、校验与 codec/repository 契约 | XML、文件路径、UI、宿主生命周期 |
| `EzNutrition.Archives.Xml` | Contracts 的版本化 XML 编解码与安全读取 | 应用用例、WPF/浏览器存储、临床计算 |
| `EzNutrition.Shared` | 客户端与服务端共同认可的 HTTP DTO、参考数据记录形状和授权策略 | 页面、宿主服务、桌面 API；也不应仅因代码“通用”就放入此处 |
| `EzNutrition.Domain` | 咨询、评估、膳食与营养计算规则 | 网络、持久化、UI 和具体档案格式 |
| `EzNutrition.Application` | 用例编排、咨询工作区、档案流程与外部能力端口 | HTTP、WebView、IndexedDB、Windows 文件系统 |
| `EzNutrition.UI` | 可单独复用和渲染测试的传输无关 Razor 组件 | HttpClient、认证令牌、具体宿主或 XML codec |
| `EzNutrition.Presentation` | 共享 App/Router、页面、布局、会话、可选登录信息存储端口、客户端 HTTP/SSE 适配与公共静态资源 | DPAPI、证书绕过、IndexedDB、文件对话框、WPF Shell、宿主启动代码 |
| `EzNutrition/Client` | WASM 启动与组合根、IndexedDB/浏览器文件交互、浏览器入口页和 JavaScript | WPF 类型、共享页面和共享 HTTP 实现 |
| `EzNutrition.Wpf` | WPF 启动与组合根、本机档案、用户连接设置、DPAPI、桌面证书策略、Windows 对话框、Shell 和 WebView2 生命周期 | WASM 类型、浏览器存储、领域计算复制品 |
| `EzNutrition.Server` | API、认证、参考数据访问、AI 调用与审计；托管 WASM 静态发布资源 | WPF 或本地档案实现 |
| `EzNutrition.AiAgency` | 服务端模型供应商适配 | 客户端页面、宿主档案和领域计算 |

## 两个需要保留判断的边界

`EzNutrition.Shared` 当前同时包含传输 DTO、参考数据记录形状和授权策略，且 Domain、Server 和客户端都已依赖它。这个名称偏宽，依赖方向也不是理想的纯领域叶节点；但拆分会影响服务端序列化、参考数据访问和既有 Domain 输入类型。当前 WPF 目标不修改它，也不把新的宿主代码放进去。只有在明确迁移协议和兼容方案后，才应单独讨论进一步拆分。

`EzNutrition.Presentation` 中部分完整页面仍直接使用 Domain 或 Archives.Contracts 类型。这是现有工作台外壳的现实边界，不妨碍宿主独立；当某段逻辑形成稳定用例时，应优先下沉到 Application，而不是为了消除引用机械地增加转发接口。

`EzNutrition.Server → EzNutrition.Client` 是 hosted Blazor WebAssembly 的发布关系：Server 借此收集并提供 WASM 静态资源。Client 不引用 Server，WPF 也不参与该关系，因此它不构成两个客户端宿主互相依赖。

## 自动保护

架构测试会验证：

- UI 不引用 Presentation、具体宿主或 HTTP；
- Presentation 不引用 Client、WPF、Server 或 WebAssembly 运行时；
- WPF 引用 Presentation 而不引用 Client；
- WASM 引用 Presentation 而不引用 WPF；
- DPAPI、用户设置文件和自签名证书策略只由 WPF 提供；
- 浏览器与桌面档案适配器继续分别归属各自宿主。

新增跨宿主能力时，应先判断它属于 Domain 规则、Application 用例、UI 组件、Presentation 工作台，还是宿主适配器，再选择项目；项目名称中的 `Shared` 本身不是归属依据。
