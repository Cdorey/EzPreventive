# 本机量表 PDF 模板

DRIs 独立速查使用 `dri-evaluation.mjs` 模板版本 1，包含逐项参考值、指标类型、单位、来源说明及聚合冲突。它始终输出评估稿，不生成报告编号。两种模板通过 `report-pdf.mjs` 共用本机引擎、字体、表格基础样式和评估水印；各自保留正文排版。

`assessment-report.mjs` 是浏览器和 WPF 共用的模板版本 3：咨询报告页脚显示报告编号与修订号；独立速查显示未关联咨询档案，不生成正式编号。依赖、字体和图片只允许使用随应用发布的资源；不得引入远程报告转换、患者资料上传或远程字体服务。历史报告继续使用原 PDF，不因模板升级而重排。

## 固定依赖

| 文件 | 来源 | SHA-256 |
| --- | --- | --- |
| `vendor/pdfmake.min.js` | pdfmake 0.3.11，npm 正式发布包，MIT | `faaee53f8dcf48b0934553665809d8180e20e435b654f633df2f18f9dbdeaa88` |
| `fonts/NotoSansCJKsc-Regular.otf` | notofonts/noto-cjk，提交 `f8d157532fbfaeda587e826d4cd5b21a49186f7c`，SIL OFL 1.1 | `2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b` |

许可证随上述文件保存。字体约 16 MB，按需加载，PDF 只嵌入实际使用字形。使用完整字体覆盖患者姓名，暂不按常用字裁剪源字体。首版仅使用一种字重，层级由字号与间距表达。

开发时可运行仓库根目录的 `python tools/reports/fetch_assets.py` 重建依赖。脚本固定 npm 包完整性值及字体仓库提交；这不是应用运行时步骤。更新依赖或版式时应复核兼容性并递增模板版本，历史报告只能读取已保存原件。

## 验证

`node tools/reports/verify-pdf.mjs` 使用 Playwright 和本机 Edge，在仅允许本地资源的环境中生成正式、评估和多页样本至 `tmp/reports/`。需配置本机 Node 能解析 Playwright。脚本不连接应用账号、不使用真实患者，也不实际向打印机发送任务。

随后使用 Poppler 渲染各页，并检查中文、长文本、表头、水印、签发页脚和分页。PDF 字节生成成功不能代替视觉验收。

`python tools/reports/check-pdf.py` 检查上述样本页数、逐项内容和每页标记。

启动本机 Client 开发宿主后，可运行 `node tools/reports/verify-browser.mjs http://127.0.0.1:5186 must` 和 `python tools/reports/check-browser.py must`。最后一个参数也支持 `nrs-2002`、`mna-sf`，结果分别保存在 `tmp/reports/<量表编码>/`。测试在独立浏览器会话中拦截认证与参考接口，使用合成资料实际作答、打印评估稿、签发归档、刷新调阅并打开 PDF 打印窗口；核对评分、水印、正式档案及窗口原件的一致性。它不向实体打印机发送任务。修改并重新构建 WASM 后，应先重启开发宿主以更新静态资源映射。

Windows 桌面运行验收使用 `dotnet run --project tools/reports/WpfProbe/WpfProbe.csproj -- tmp/reports/must/browser-printed.pdf tmp/reports/wpf`。该工具需要 Windows 桌面会话、SDK 和 WebView2 Runtime；在指定输出目录建立隔离的 WebView2 配置，使用产品中的实际 PDF 窗口，核对宿主交付的 PDF 流并通过可访问性树确认打印对话框出现。支持中文或英文系统，截图用于人工检查；不提交打印任务。它通过反射访问内部窗口，窗口重构时应同步更新工具，不为此扩大产品 API。

WebView2 采用官方 [ShowPrintUI 路线](https://learn.microsoft.com/en-us/microsoft-edge/webview2/how-to/print)。内容截图不包含原生打印对话框，因此不能单凭截图认定打印交互成功。

`dotnet run --project tools/reports/WpfProbe/WpfProbe.csproj -- --generate tmp/reports/wpf-generation` 用于验证 WPF Blazor 内生成 PDF：探针复用产品宿主的静态资源清单和真实 PDF 适配器，以合成输入生成三种量表正式/评估稿和 DRIs 评估稿。它记录资源请求、成品摘要及失败诊断，不用模拟 JS 字节代替实际生成。使用新的输出目录，并检查进程成功退出及当次文件，不能只检查旧文件是否存在。

`python tools/reports/check-wpf-generation.py <输出目录>` 核对七份合成 PDF 的内容、分值、水印、字体、摘要及本机请求。它只检查成品，不能代替进程正常退出与视觉验收。

2026-09-09 在 Active 桌面会话已生成七份合格 PDF，但探针退出时挂起。报告适配器已改为在生成操作内释放 JS 模块句柄；预览主动关闭时释放 Blob，组件移除时由 MutationObserver 回收，组件 Dispose 不再等待 JS。`verify-pdf.mjs` 同时验证预览回收及过期组件保护，PDF 模板版本不变，因为版式和成品内容未改动。

修改后曾因会话重新断开而在图形初始化报 `0x8876086A`；会话再次恢复 Active 后，以新目录 `tmp/reports/wpf-generation-complete` 复验，七份 PDF 生成、控件释放和进程退出均成功，当次内容与全部页面视觉检查通过。今后复验仍需正常桌面会话、新输出目录和成功退出，不能仅凭旧文件判断。探针的生成与释放均有有限等待时间，超时按失败处理。完整证据见 `docs/report-acceptance-audit.md`。

`node tools/reports/verify-storage.mjs` 在隔离的真实 IndexedDB 中验证两页面竞争提交、旧预览拒绝及事务中止后的索引和正文一致性，不依赖运行中的应用宿主。

浏览器脚本追加 `--revision`（目前使用 `must`）可验证初版签发后更正、刷新重印及历史原件保留；配套运行 `python tools/reports/check-browser.py must --revision`。结果位于 `tmp/reports/must-revision/`。

浏览器脚本追加 `--standalone` 可验证三种试行量表的独立速查打印；使用仅有打印权限的会话，分别输出未完成及完整结果，检查没有任何档案写入。配套运行 `python tools/reports/check-browser.py must --standalone`（也支持另两种编码）。

`node tools/reports/verify-browser.mjs http://127.0.0.1:5186 dris` 使用合成参考记录验证 DRIs 多页输出、查询失败和空结果时清除旧打印入口，以及打印不触发新查询或档案写入。运行 `python tools/reports/check-browser.py dris` 核对内容及每页水印，再用 Poppler 检查所有页面。
