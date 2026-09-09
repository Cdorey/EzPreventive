# 报告签发与打印验收审计

日期：2026-09-09。分支：`codex/report-issuance-printing`。

本审计以[工作方案](report-issuance-printing-plan.md)和实际代码、测试、生成文件为依据。约定的首版试行功能已实现并完成验收；下文列出的业务待定项按用户授权保留讨论。最后的 WPF 生成及退出复验在 Active 桌面会话通过：产品适配器生成七份合成 PDF，控件正常释放，进程退出码为 0；当次成品内容及全部页面视觉检查通过。

运行环境曾在同一 Windows 会话 10 的 `Active` 与 `Disconnected` 间变化：Active 时生成成功；生命周期修改后的复验又在 `CreateD3D9Device` 报 `0x8876086A`，随后 `query session` 确认会话 10 为 Disconnected、控制台 9 为 Connected。没有切换、连接或修改任何 Windows 会话，也不将断开会话的图形初始化失败推断为正常桌面的产品缺陷。

释放挂起的代码路径有明确依据：当前依赖的 [BlazorWebView.DisposeAsync](https://github.com/dotnet/maui/blob/10.0.100/src/BlazorWebView/src/Wpf/BlazorWebView.cs) 先调用 MarkAsDisposing；[WebView2WebViewManager.SendMessage](https://github.com/dotnet/maui/blob/10.0.100/src/BlazorWebView/src/SharedSource/WebView2WebViewManager.cs) 此后忽略消息。报告生成器此前在服务释放时等待 JS 模块释放，存在无法收到回应的路径。现改为每次生成操作内持有并释放模块句柄，浏览器仍缓存模块；预览 Blob 由 JS 观察组件移除并回收，组件 Dispose 不再调用 JS。探针为控件释放增加 15 秒失败期限，避免无限等待。浏览器的主动关闭、组件移除、过期组件保护及签发更正回归已通过；最后的桌面复验也确认正常生成及退出，不再将生成前失败或旧成品当作此次成功证据。

## 需求与证据

| 需求 | 已检查的实现和证据 | 结论 |
| --- | --- | --- |
| 独立的签发、打印权限，沿用前端会话 | `PolicyList` 注册两个 Permission；`ReportAuthorization` 使用 `UserSessionService` 和同一 `IAuthorizationService`。`ReportAuthorizationTests` 用真实 JWT 解析、policy 和受控会话验证四种组合、姓名缺失、退出及会话到期；不从医师角色或处方权限推导 | 通过；实际资质核验及授权由管理员执行 |
| 报告操作不发送患者数据至服务端 | Application 没有新增报告 HTTP 端口；模板只访问随应用发布的资源。浏览器完整流程记录请求，并拒绝报告阶段的新业务请求或外部请求；使用合成账号和参考数据 | 浏览器已验证；登录、既有参考查询和用户主动 AI 操作保持原边界 |
| 先试行 NRS 2002、MNA-SF、MUST，模块分别提供签发和打印 | `MainTreatment`、`AssessmentReportPanel` 的三种入口；浏览器实际作答、评估打印、审核签发、刷新后档案重印，PDF 文本检查覆盖三种结果 | 通过；其他正式报告模板没有自动开放 |
| 未签发输出有水印，正式输出使用干净原件 | 模板按评估用途生成逐页水印及固定说明；`PrintEvaluationAsync` 拒绝未保存的正式预览；`PrintStoredAsync` 读取已保存 PDF | 通过；拥有签发权限不会自动去除评估稿水印 |
| 冻结所选量表及输入，审核前后内容一致 | `AssessmentReportFactory`、`AssessmentReportDraft` 和 `ArchiveContractAssembler` 捕获独立版本；`PreparedAssessmentReport` 绑定预览字节；确认再次核对签发人。`AssessmentReportTests` 覆盖范围、版本、评分对象资料和后续修改隔离 | 通过；只确认报告，不提升整次咨询状态 |
| 正式报告有档案记录及可重复输出的成品 | `.ezreport` 包保存 `NutritionReport`、输入快照和 PDF；报告文档键与咨询草稿分离。浏览器捕获打印窗口 PDF，与包内原件逐字节比较，并核对契约中的 SHA-256 | 通过；摘要用于一致性检查，不是电子签章 |
| 重印的字体与排版稳定 | PDF 自带字体；`check-pdf.py` 逐页检查字体描述符中的嵌入字体数据。真实模板正式/评估单页和六页长表格样本已生成，中文、分页、表头及每页水印已检查 | 通过；实体设备的纸张、缩放、色差仍由打印设置决定 |
| 保存失败、重试、多窗口竞争不破坏旧报告 | `ReportWorkflow.CommitAsync` 使用宿主 CompareExchange；IndexedDB 同一事务提交索引与正文；WPF 使用写锁、不同内容文件及索引替换作为提交点。真实 IndexedDB 竞争/中止脚本及 WPF 文件存储测试覆盖失败保留、过期预览和并发赢家 | 通过 |
| 更正复用现有版本链，不追加第二套状态 | 草稿 `BasedOn`，确认后 `Amended`/`Supersedes`；新版成功提交才替代旧版；历史版本及每份 PDF 保留。测试覆盖三版交换、错误评估实例、失败和过期预览 | 通过；更正需要原咨询及原评估仍在工作区 |
| 导入、导出、兼容和冲突处理 | `ReportPackage` 兼容版本 1/2，限制 ZIP 条目及解压大小，校验每份 PDF 和完整引用；更旧、分叉及改写本机历史的包不能覆盖。报告导出复用 PrintReport；通用档案导出不能绕过 | 通过；未新增患者数据传输接口 |
| 档案库区分报告和咨询 | `ArchiveCenter` 区分计数、原件打印与导入；报告不进入复诊历史。审计增加了合法报告引用多个患者版本的用例，修复调阅失败并验证采用确切咨询对象快照 | 通过 |
| 独立速查可输出评估稿，不虚构咨询档案 | 三种量表速查捕获已有答案；DRIs 在查询成功时捕获条件和结果。单元测试及浏览器验证无档案写入、无重查、未回答标记、DRIs 上下限/冲突，以及空结果和失败清除旧入口 | 通过 |
| 共享模板与宿主适配遵循包边界 | Common 保持题目、计分和业务解释；Application 提供快照与流程；Presentation 提供 PDF、字体及页面连接；Client/WPF 适配本机存储与打印。现有架构测试已执行 | 通过；没有新建通用报告框架或修改 Common 的排版职责 |
| WASM 发布产物实际可运行 | 本地 `dotnet publish` 成功后，以本机静态服务运行真实发布目录，复验三种正式量表、MUST 更正/速查及 DRIs 多页输出，核对 PDF 内容与原件字节 | 通过；未部署到外部服务 |
| WPF 查看和打印实际交互 | `WpfProbe <合成PDF> <目录>` 运行产品实际 PDF 窗口，核对交付流、捕获预览并从可访问性树确认打印对话框；量表及 DRIs 均通过 | 通过；没有提交实体打印任务 |
| WPF Blazor 内实际生成 PDF | 生命周期修正后以新目录 `wpf-generation-complete` 运行真实 BlazorWebView 和产品适配器；七份内容、分值、水印、字体嵌入、摘要及本机请求检查通过，全部页面经 Poppler 渲染并检查。控制台确认控件释放完成，退出码 0。此前桌面生成的 MUST 原件也已交给实际 WPF 查看器，字节一致且打印对话框出现 | 通过；正常生成及退出均有当次运行证据 |

## 验证入口

生命周期修正后的最终全解决方案 Release 构建成功（0 错误，保留既有 2FA 提示），680 项测试通过：Common 26、Contracts 79、XML 8、Application 82、Client 245、WPF 65、Server 175，无失败或跳过。真实浏览器 MUST 签发更正重印、DRIs 输出及预览回收检查通过。桌面生成探针独立计结果，不包含在单元测试总数中。

- `dotnet build EzPreventive.sln -c Release --no-restore`
- `dotnet test EzPreventive.sln -c Release --no-build --no-restore`
- `node tools/reports/verify-pdf.mjs`，随后 `python tools/reports/check-pdf.py` 和 Poppler 逐页检查。
- `node tools/reports/verify-browser.mjs <本机地址> must|nrs-2002|mna-sf|dris`；MUST 支持 `--revision`，三种量表支持 `--standalone`。配套 `check-browser.py` 检查字节、摘要、内容和水印。
- `node tools/reports/verify-storage.mjs` 验证真实 IndexedDB 原子提交。
- `dotnet run --project tools/reports/WpfProbe/WpfProbe.csproj -- <合成报告.pdf> <输出目录>` 验证桌面原件查看与打印交互。
- `dotnet run --project tools/reports/WpfProbe/WpfProbe.csproj -- --generate <新输出目录>` 验证桌面 Blazor 生成。必须以命令成功退出、当次生成文件和请求记录共同判断，已在修正后通过。
- `python tools/reports/check-wpf-generation.py <输出目录>` 检查七份合成成品、分值、水印、嵌入字体、摘要和本机请求；这条检查不证明探针正常退出。

最后通过的桌面命令：`dotnet run --project tools/reports/WpfProbe/WpfProbe.csproj --no-restore -- --generate tmp/reports/wpf-generation-complete`。七份当次 PDF（三种量表各正式/评估两份，加 DRIs 评估一份）、`requests.txt` 与摘要记录均通过检查，逐页核对了中文、分值、水印及边界。探针不登录真实账号，不发送实体打印任务。

所有运行输入均为合成资料。生成文件位于忽略目录 `tmp/reports/`；验证脚本和说明纳入仓库，患者资料不作为测试夹具提交。

## 保留讨论的业务边界

用户允许把不适宜自行决定的特性留待讨论，以下事项没有用技术默认值替代业务决定：

1. 独立作废：没有后继报告时如何作废，谁可操作，是否需说明及历史状态版本；当前不开放入口。
2. 已被替代的旧版输出：历史 PDF 保留，但普通重印只输出当前版本。是否提供原件查看、带“已被替代”标记的副本及对应权限，仍需决定。
3. 正式报告删除和整库清空：当前保护正式报告，不通过清空绕过待定的删除规则。
4. 其他模块的正式报告、图表类模板、咨询总报告、SOAP 和 AI 正文：本轮正式报告以三种量表试行为范围，后续需明确各自内容与签发条件。

操作与备份说明见[使用说明](report-user-guide.md)。本模块没有实现身份独立认证、密码学电子签章或自动免责机制。
