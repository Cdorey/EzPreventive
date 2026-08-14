# 档案架构

EzNutrition 的档案能力遵循“语义、用例、格式、宿主”四个边界，避免 UI 或 XML 实现决定未来 WPF Hybrid 和医疗机构适配器的形状。

## 依赖方向

```text
EzNutrition.UI
    -> EzNutrition.Application (IArchiveWorkflow、只读调阅模型)

EzNutrition.Application
    -> EzNutrition.Archives.Contracts
    -> IArchiveDocumentStore / IArchiveDocumentTransport（宿主端口）

EzNutrition.Archives.Xml
    -> EzNutrition.Archives.Contracts

EzNutrition.Client 或未来 WPF 宿主
    -> Application + UI + Xml
    -> 实现宿主存储和文件交互端口
```

`EzNutrition.UI` 不引用 XML codec、浏览器存储或文件路径。`EzNutrition.Archives.Xml` 不引用 Application、UI、Client、浏览器 API 或桌面文件 API。

## 操作语义

- **保存档案**：由 `IArchiveWorkflow` 将当前咨询映射成契约文档、校验、编码后交给 `IArchiveDocumentStore`。WASM 当前使用 IndexedDB；未来 WPF 或机构适配器可以使用文件目录、SQLite 或医院数据源。
- **档案调阅**：列出宿主管理的档案摘要，解码选中文档并投影为格式无关的只读模型。当前不会把历史文档恢复成可编辑计算工作区。
- **打开文档**：由 `IArchiveDocumentTransport` 取得外部文档，再选择可读 codec。它不会自动写入本机档案库。
- **导出文档**：把当前咨询写成外部文档；文件名不包含患者姓名或业务标识。
- **删除或清空**：属于宿主可选能力，不由 XML codec 决定。UI 只在宿主声明能力时提供入口，Application 在每次调用时仍处理权限拒绝；浏览器宿主用同一个 IndexedDB 事务同时删除摘要和 XML 字节。

XML 文档和浏览器 IndexedDB 都不会因为档案操作而上传到服务端。应用中参考数据查询与 AI 建议仍有各自独立的网络和隐私边界。

WASM 宿主在 IndexedDB 中将轻量文档摘要与 XML 字节分别保存。浏览档案只读取摘要，选择具体咨询或开始后续咨询时才读取并解码对应 XML；两个部分由同一个 IndexedDB 事务原子写入。旧版内联保存的文档在数据库升级时原子迁移，不改变 Application、UI 或 XML codec 的接口和语义。

## 时间语义

档案契约使用 `DateTimeOffset` 表示绝对时刻；应用新建的审计时间使用 UTC，导入文档中已有的明确偏移则被忠实保留。XML、存储和 Application 不按当前设备时区改写档案时间。

本地时间转换只发生在最终 UI 渲染阶段。`EzNutrition.UI` 通过 `ILocalDateTimeFormatter` 获取宿主选择的显示时区；WASM 默认使用浏览器设备时区，未来 WPF 或机构宿主可以注入系统时区或机构配置时区。机器可读的 HTML `datetime` 属性仍使用 UTC，面向医务人员的可见文本只显示转换后的当地日期和时间，不额外展示 UTC 偏移。

## 校验边界

档案校验只负责安全、结构、生命周期、引用闭包、来源说明，以及能够由文档自身确定性复算的内部完整性。它不判断临床数值或状态组合是否合理，也不比较不同临床算法或数据口径孰优孰劣；只要契约能够表达，异常、矛盾或不完整的临床事实也应被忠实保存和还原。

例如负数测量、人口学信息与生理状态的罕见组合、空 SOAP 内容，以及食品成分表记录能量与 4/9/4 折算能量的差异，都不由档案校验器生成提示。专业复核、诊断和数据质量分析属于独立的 Application/Domain 服务。相反，悬空引用、重复资源版本、值与缺失原因同时存在、汇总快照无法由其保存的组成项复算，以及声明为确定性算法的派生值无法重现，仍属于档案内部完整性错误。

## Bundle 与患者

当前保存单位是 `ConsultationDocument`：一个 Patient、一次 Consultation 和该咨询的引用闭包。Bundle 是版本化文档/传输容器，不是“患者永久聚合根”。同一次运行态咨询重复保存会更新自己的草稿文档；开始后续咨询则建立新的 Consultation、临床资源和 Bundle，既有文档始终只读。

Patient 的逻辑标识跨咨询保持稳定，本机档案索引以该标识归组多次 Consultation。档案页的“新建后续咨询”会从最近一次文档恢复患者身份，并仅用当次 SubjectSnapshot 预填新咨询表单；医生仍需按本次实际情况核对年龄、测量和生理状态。Patient 主数据不会根据咨询表单被隐式改写，如需更名或更正身份，应另行设计显式的患者资料维护和版本规则。

归组不使用姓名推断。相同姓名但 Patient 标识不同的档案始终分开；旧版没有 Patient 索引的文档也各自保持独立，不开放后续咨询入口。未来如需交换完整患者纵向档案，应新增明确的患者档案 profile 和引用闭包校验，不应悄悄改变 `ConsultationDocument` 的含义。

## 扩展与二开

医疗机构通常只需实现：

- `IArchiveDocumentStore`：保存、列出和读取编码档案文档；
- `IArchiveDocumentTransport`：宿主文件或文档交互；
- 或更底层的 `IArchiveRepository`：资源版本、并发和历史语义。

机构字段优先使用 `ArchiveExtension`。新增自定义 `IArchiveResource` 类型还需要对应 codec 与校验扩展，不能假设默认 XML codec 会自动认识任意 CLR 类型。

## XML 安全与兼容

XML codec 禁止 DTD 和外部实体，限制文档字符数与元素深度，在完整编码成功前不写入调用方目标流。读取后的未知源内容由 XML 专用 `ArchiveRoundTripState` 保留：语义未变化时可以原样回写；若已知语义发生变化，则返回兼容性错误，避免静默丢失未知内容。

XML 是交换编码，不默认提供静态加密。需要加密、签名、密钥管理或审计时，应由外部安全容器或机构宿主策略承担。
