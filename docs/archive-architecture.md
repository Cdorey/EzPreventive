# 档案架构

EzNutrition 的档案能力遵循“语义、用例、格式、宿主”四个边界，避免 UI 或 XML 实现决定 WPF Hybrid 和医疗机构适配器的形状。

## 依赖方向

```text
EzNutrition.UI
    -> EzNutrition.Application (IArchiveWorkflow、只读调阅模型)

EzNutrition.Presentation
    -> EzNutrition.UI + EzNutrition.Application
    -> 共享 App、页面、会话和客户端 HTTP 适配

EzNutrition.Application
    -> EzNutrition.Archives.Contracts
    -> IArchiveDocumentStore / IArchiveDocumentTransport（宿主端口）

EzNutrition.Archives.Xml
    -> EzNutrition.Archives.Contracts

EzNutrition.Client 或 EzNutrition.Wpf（互不引用）
    -> Presentation + Application + Xml
    -> 实现宿主存储和文件交互端口
```

`EzNutrition.UI` 不引用 XML codec、浏览器存储或文件路径。`EzNutrition.Archives.Xml` 不引用 Application、UI、Client、浏览器 API 或桌面文件 API。

## 操作语义

- **保存档案**：由 `IArchiveWorkflow` 将当前咨询映射成契约文档、校验、编码后交给 `IArchiveDocumentStore`。WASM 使用 IndexedDB，WPF 使用当前用户的本地应用数据目录；机构适配器仍可使用受控文件目录、SQLite 或医院数据源。
- **档案调阅**：列出宿主管理的档案摘要，解码选中文档并投影为格式无关的只读模型。当前不会把历史文档恢复成可编辑计算工作区。
- **打开文档**：由 `IArchiveDocumentTransport` 取得外部文档，再选择可读 codec。它不会自动写入本机档案库。
- **导出文档**：把当前咨询写成外部文档；文件名不包含患者姓名或业务标识。
- **删除或清空**：属于宿主可选能力，不由 XML codec 决定。UI 只在宿主声明能力时提供入口，Application 在每次调用时仍处理权限拒绝；浏览器宿主用同一个 IndexedDB 事务同时删除摘要和 XML 字节，WPF 只删除文件名与索引均属于其管理边界的内容。

XML 文档和浏览器 IndexedDB 都不会因为档案操作而上传到服务端。应用中参考数据查询与 AI 建议仍有各自独立的网络和隐私边界。

WASM 宿主在 IndexedDB 中将轻量文档摘要与 XML 字节分别保存。浏览档案只读取摘要，选择具体咨询或开始后续咨询时才读取并解码对应 XML；两个部分由同一个 IndexedDB 事务原子写入。旧版内联保存的文档在数据库升级时原子迁移，不改变 Application、UI 或 XML codec 的接口和语义。

WPF 宿主在 `%LOCALAPPDATA%\EzSuit\EzNutrition\Archives` 保存以文档 GUID 命名的 XML，并在隐藏的 `.catalog` 目录保存调阅摘要。外部打开使用 Windows 文件选择器；导出使用“另存为”，成功后由资源管理器选中新文件。详细运行和备份边界见 [WPF Hybrid 宿主](./wpf-hybrid-host.md)。

## 时间语义

档案契约使用 `DateTimeOffset` 表示绝对时刻；应用新建的审计时间使用 UTC，导入文档中已有的明确偏移则被忠实保留。XML、存储和 Application 不按当前设备时区改写档案时间。

本地时间转换只发生在最终 UI 渲染阶段。`EzNutrition.UI` 通过 `ILocalDateTimeFormatter` 获取宿主选择的显示时区；WASM 使用浏览器设备时区，WPF 注入 Windows 系统时区，机构宿主可以注入机构配置时区。机器可读的 HTML `datetime` 属性仍使用 UTC，面向医务人员的可见文本只显示转换后的当地日期和时间，不额外展示 UTC 偏移。

## 年龄与出生日期

运行态使用结构化实足年龄保存完整年、月和日，并以可空月日区分“未知精度”和“明确为零”。门诊录入允许医生在完整出生日期与直接填写整岁之间二选一；出生日期模式使用当次录入日期计算复合年龄，整岁模式不虚构月日。

`Patient.BirthDate` 保存可选出生日期，`Consultation.SubjectSnapshot.ChronologicalAgeAtConsultation` 保存本次咨询实际采用的年龄快照。旧版 `AgeAtConsultation` 数量字段继续保存完整年数作为兼容降级值；新代码优先读取结构化字段，两个字段同时存在时必须一致。

EER、DRIs 和膳食宝塔仍使用十进制年匹配既有年龄阈值。Application 将结构化年龄单向投影为十进制年后交给数据源；该投影会作为计算输入进入档案，但不能用于反向重建原始年月日。服务端 API 和参考数据表因此无需感知 UI 或档案中的复合年龄结构。

## 校验边界

档案校验只负责安全、结构、生命周期、引用闭包、来源说明，以及能够由文档自身确定性复算的内部完整性。它不判断临床数值或状态组合是否合理，也不比较不同临床算法或数据口径孰优孰劣；只要契约能够表达，异常、矛盾或不完整的临床事实也应被忠实保存和还原。

例如负数测量、人口学信息与生理状态的罕见组合、空 SOAP 内容，以及食品成分表记录能量与 4/9/4 折算能量的差异，都不由档案校验器生成提示。专业复核、诊断和数据质量分析属于独立的 Application/Domain 服务。相反，悬空引用、重复资源版本、值与缺失原因同时存在、汇总快照无法由其保存的组成项复算，以及声明为确定性算法的派生值无法重现，仍属于档案内部完整性错误。

## Bundle 与患者

当前保存单位是 `ConsultationDocument`：一个 Patient、一次 Consultation 和该咨询的引用闭包。Bundle 是版本化文档/传输容器，不是“患者永久聚合根”。同一次运行态咨询重复保存会更新自己的草稿文档；开始后续咨询则建立新的 Consultation、临床资源和 Bundle，既有文档始终只读。

Patient 的逻辑标识跨咨询保持稳定，本机档案索引以该标识归组多次 Consultation。档案页的“新建后续咨询”会从最近一次文档恢复患者身份，并仅用当次 SubjectSnapshot 预填新咨询表单；医生仍需按本次实际情况核对年龄、测量和生理状态。Patient 主数据不会根据咨询表单被隐式改写，如需更名或更正身份，应另行设计显式的患者资料维护和版本规则。

归组不使用姓名推断。相同姓名但 Patient 标识不同的档案始终分开；旧版没有 Patient 索引的文档也各自保持独立，不开放后续咨询入口。未来如需交换完整患者纵向档案，应新增明确的患者档案 profile 和引用闭包校验，不应悄悄改变 `ConsultationDocument` 的含义。

## 报告与责任主体

`NutritionReport` 是报告清单和签发上下文，不是打印实现。它保存医疗或教学等用途编码、形成内容的确切资源版本、可选版式模板、作者/复核者/监督者等参与事实，以及可选的外部渲染产物身份；XML 不内嵌 PDF、HTML 或打印字节。

草稿可以没有复核者和渲染产物。正式或修订报告继续使用通用 `ResourceMetadata.FinalizedAt` 与 `FinalizedBy` 表示签发，并必须用媒体类型和内容指纹绑定用户实际看到的成品。复核者不是强制字段，同一主体可以同时作为作者和签发者；其是否具备医师、营养师或教师资格，由当时的业务用例和授权策略判断，不由格式无关档案校验器推断。

`ActorReference.Kind` 可保存医师、营养师、护士、教师或学生等行为时主体种类，`Organization` 可保存该主体在本次行为中代表或所属的机构快照。两者都是历史事实，不是当前账号资料；JWT Claims、角色和 Permission 不进入档案契约，也不能从档案字段反向授予权限。

## 通用营养量表评估

`NutritionScaleAssessment` 为结构相近的营养筛查或评估量表提供统一档案形状。它保存量表的版本化身份或定义指纹、题目编码与类型化回答、可选分值贡献、推导结果、总分、解释、评分方法、实施者和确切输入资源引用。档案只保存一次评估的事实快照，不保存动态表单布局，也不充当量表定义注册中心。

NRS 2002、MNA 等具体量表的题目约束、跳题条件、计分和专业解释应由各自的 Domain/Application 实现，再映射成该通用资源。格式无关校验器只检查身份可追溯性、回答与缺失原因、引用闭包和结果结构，不假设所有量表都采用简单加法，也不判断分值的临床合理性。因此，新增结构相近的量表通常不需要新增 CLR 档案资源或修改 XML codec；只有无法由“量表—回答—结果”结构忠实表达的复杂评估才应建立专用资源。

当前运行态组装器尚不产生通用量表资源。首次接入具体量表时应同时补充其领域模型、业务校验、契约映射和调阅投影，而不把这些规则反向塞入 Contracts 或 XML。

## 扩展与二开

医疗机构通常只需实现：

- `IArchiveDocumentStore`：保存、列出和读取编码档案文档；
- `IArchiveDocumentTransport`：宿主文件或文档交互；
- 或更底层的 `IArchiveRepository`：资源版本、并发和历史语义。

机构字段优先使用 `ArchiveExtension`。新增自定义 `IArchiveResource` 类型还需要对应 codec 与校验扩展，不能假设默认 XML codec 会自动认识任意 CLR 类型。

## XML 安全与兼容

XML codec 禁止 DTD 和外部实体，限制文档字符数与元素深度，在完整编码成功前不写入调用方目标流。读取后的未知源内容由 XML 专用 `ArchiveRoundTripState` 保留：语义未变化时可以原样回写；若已知语义发生变化，则返回兼容性错误，避免静默丢失未知内容。

格式版本标识 XML 信封和通用编码结构；codec 对资源 profile 的支持单独判断。增加 `NutritionReport` 或 `NutritionScaleAssessment` 不改变既有资源的 1.0 编码，无法识别相应资源的旧 codec 会返回明确的“不支持资源”错误，而不会把内容静默丢弃。

XML 是交换编码，不默认提供静态加密。需要加密、签名、密钥管理或审计时，应由外部安全容器或机构宿主策略承担。
