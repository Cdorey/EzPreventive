# 本机量表 PDF 模板

`assessment-report.mjs` 是浏览器和 WPF 共用的模板版本 1。依赖、字体和图片只允许使用随应用发布的资源；不得引入远程报告转换、患者资料上传或远程字体服务。

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
