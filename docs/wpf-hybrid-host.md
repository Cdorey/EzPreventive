# WPF Hybrid 宿主

`EzNutrition.Wpf` 是 EzNutrition 的 Windows 本地宿主。它使用 WPF 提供窗口、文件对话框和 Windows Shell 集成，使用 `BlazorWebView` 承载现有 Razor 工作台。Razor 组件在桌面进程中的 .NET 运行时执行，不在 WebAssembly 中执行。

宿主的组合根复用 `EzNutrition.Presentation`、`EzNutrition.Application`、`EzNutrition.Archives.Contracts` 和 `EzNutrition.Archives.Xml`。共享 App、页面、布局、会话和 HTTP 适配位于 Presentation Razor 类库；WPF 与 WASM 是互不引用的并列宿主。WPF 项目只实现文件系统、文档交互、系统时区和窗口生命周期等宿主职责，不复制营养计算，也不改变 Domain 或服务端业务规则。

## 本机档案目录

默认档案根目录是：

```text
%LOCALAPPDATA%\EzSuit\EzNutrition\Archives
```

这里有意不使用 `Documents`：文档目录经常被 OneDrive 或机构同步策略自动接管，而营养咨询档案可能包含敏感信息。`LocalApplicationData` 是当前 Windows 用户的应用数据边界，适合由程序管理、不会默认出现在日常文档列表中的本机档案。这个选择不能替代磁盘加密、Windows 账户保护或机构数据治理。

目录结构如下：

```text
Archives\
├── {consultation-id}.xml
└── .catalog\
    └── {consultation-id}.json
```

- XML 文件使用咨询文档 GUID 命名，不把患者姓名写进文件名。
- `.catalog` 保存调阅列表需要的格式无关摘要，包括标题、咨询对象显示文本、保存时间和格式信息；它被标记为隐藏目录，但没有加密。
- 单份文档上限为 16 MiB。读取在同一文件句柄上校验长度，写入先落到同目录临时文件并刷新到磁盘，再替换最终文件。
- 保存同一咨询时会替换对应文档；清空档案库还会清理由中断写入留下、且文件名可被严格识别为宿主管理内容的孤立文档，不会删除无关文件。

可以通过配置覆盖档案根目录。覆盖值必须是绝对路径，且不能是磁盘根目录：

```powershell
$env:EzNutrition__ArchiveRootPath = 'D:\ProtectedData\EzNutrition\Archives'
dotnet run --project .\EzNutrition.Wpf\EzNutrition.Wpf.csproj
```

如果机构策略会同步或备份该目录，应先确认目的地、访问控制、保留期限和数据处理依据。

## 保存、打开与导出语义

- **保存档案**写入宿主管理目录，用于应用内快速调阅。保存后不自动打开资源管理器，避免把内部目录误当作日常文档目录。
- **打开文档**使用 Windows 打开文件对话框读取外部 XML，仅做只读调阅，不自动复制进本机档案库。
- **导出档案**使用 Windows“另存为”对话框，默认从文档目录开始。成功写出后，资源管理器会打开并选中新文件；用户取消对话框会作为“已取消”返回，而不是误报成功。
- WPF 菜单中的 **文件 → 打开档案文件夹**用于检查、备份或迁移宿主管理目录。

建议备份或迁移时复制整个 `Archives` 目录，而不只复制 `.catalog` 或 XML 的其中一部分。外部导出的 XML 可以再次打开，但不会自动恢复为应用内已保存记录。

## 服务端与认证

WPF 页面来源是 WebView 的本地内部地址，HTTP 请求则发送到独立配置的 EzNutrition 服务端。`EzNutrition:ServerBaseAddress` 必须是绝对 HTTP(S) 地址；当前默认和开发配置均指向 `https://eznutrition.cdorey.net/`。

可用环境变量临时覆盖服务端：

```powershell
$env:EzNutrition__ServerBaseAddress = 'https://eznutrition.cdorey.net/'
dotnet run --project .\EzNutrition.Wpf\EzNutrition.Wpf.csproj
```

访问令牌只保存在当前桌面进程内存中。宿主不会把用户名、密码或 JWT 写入配置、WebView 本地存储或档案目录；关闭应用后需要重新登录。参考数据、账户操作和 AI 能力仍遵循服务端各自的网络与审计边界，本机档案操作本身不会上传 XML。

## 构建、运行与发布

开发机需要 Windows、.NET 10 SDK 和 Microsoft Edge WebView2 Runtime。Debug 模式启用 WebView 开发工具和详细 Hybrid 跟踪；Release 模式不会启用这些高频跟踪。

```powershell
dotnet restore .\EzPreventive.sln
dotnet build .\EzNutrition.Wpf\EzNutrition.Wpf.csproj -c Release --no-restore
dotnet test .\EzNutrition.Wpf.Tests\EzNutrition.Wpf.Tests.csproj -c Release --no-restore
dotnet run --project .\EzNutrition.Wpf\EzNutrition.Wpf.csproj
```

框架依赖发布示例：

```powershell
dotnet publish .\EzNutrition.Wpf\EzNutrition.Wpf.csproj `
  -c Release -r win-x64 --self-contained false
```

发布产物需要目标机安装兼容的 .NET 10 Desktop Runtime 和 WebView2 Runtime。WebView2 用户数据明确保存在 `%LOCALAPPDATA%\EzSuit\EzNutrition\WebView2`，因此安装到只读的 `Program Files` 不会要求在安装目录写缓存。

WPF 发布只收集 Presentation/UI Razor 类库及桌面运行依赖，不运行 WASM 发布链，也不包含 `EzNutrition.Client.dll` 或 `.wasm` 运行时文件。共享静态资源通过 Razor 类库的 `_content/EzNutrition.Presentation/` 路径提供。

当前仓库尚未决定安装器、代码签名、自动更新和机构级加密容器。这些属于分发与治理决策，不应通过把密钥或静态凭据写入 WPF 项目来临时解决。
