# EzNutrition 本地档案架构

状态：**架构基线已接受，V1 数据格式尚未发布**

建立日期：2026-08-07

本目录定义 EzNutrition 在 Blazor WebAssembly、Blazor Hybrid 以及医院自定义存储实现之间共享的本地档案模型。第一阶段只确定难以逆转的架构边界；具体 XML 命名空间、XSD 字段和 C# DTO 将在开放决策完成后另行提交。

## 规范性用语

本文档中的以下词语具有约束意义：

- **必须**：兼容实现不可违反。
- **应该**：除非有明确、可记录的理由，否则应当遵守。
- **可以**：实现者可自行选择。

在 V1 正式发布前，这些约束用于指导仓库内部设计；V1 发布后，已发布的规范、Schema 和兼容性样本不得原地改写。

## 已确定的原则

1. **UI 与 XML 解耦**：Razor 组件只使用类型化资源或视图模型，不直接解析 `XElement`、`XmlDocument` 或 XML 字符串。
2. **Resource/Bundle 模型**：可独立交换的数据必须是自描述资源；一个或多个资源可以组成档案 Bundle。
3. **存储无关**：文件、IndexedDB、SQLite 和医院数据库均为可替换实现，不定义档案的业务语义。
4. **XML 是交换格式**：XML 适合导入、导出和长期保存，但不强制成为所有实现的日常查询数据库。
5. **运行态与档案态分离**：当前 `Archive`、事件、HTTP 数据和 UI 状态不直接序列化。
6. **向后兼容优先**：新版本必须能够读取正式发布过的旧档案；旧版本不保证能够读取未来格式。
7. **历史可复核**：档案同时保存原始输入、专业人员修正、历史结果快照以及计算所依赖的数据版本。
8. **安全失败**：未知或未来数据不能静默丢弃；实现必须保留、只读打开或明确拒绝保存。
9. **技术校验与临床判断分离**：Schema 校验数据结构，但不枚举或禁止所有临床上罕见的组合。

## 文档导航

- [ADR-0001：Resource/Bundle 与存储无关架构](adr/0001-resource-bundle-architecture.md)
- [版本与兼容策略](versioning-policy.md)
- [扩展与未知数据策略](extension-policy.md)
- [安全基线](security-baseline.md)
- [开放决策](open-decisions.md)

## 计划中的第一条纵向链路

V1 的第一条可执行链路应覆盖一份最小但完整的营养咨询：

```text
PatientResource
    └── ConsultationResource
          ├── EnergyAssessmentResource
          ├── DietaryRecallResource
          └── SoapNoteResource

上述资源 → ArchiveBundle → XML → 重新读取 → 当前类型化模型
```

完成条件包括：

- 独立资源和 Bundle 都是格式良好的 XML 文档；
- 使用内嵌、受信任的 XSD 验证，不访问网络 Schema；
- 已知资源可往返读取，未知扩展可保留；
- 历史格式通过版本化读取器迁移到当前内存模型；
- WASM 和 Hybrid 使用相同的资源模型与 XML Codec；
- 全部兼容性规则具有固定样本和自动化测试。

## 本阶段明确不做

- 不修改生产数据库或创建 migration；
- 不实现患者档案管理 UI；
- 不创建 Blazor Hybrid 宿主；
- 不决定医院如何落盘、索引或同步；
- 不发布 V1 XSD；
- 不把当前运行态 `Archive` 直接改造成持久化 DTO；
- 不宣称完整兼容 HL7 FHIR。

这些限制是为了先稳定公共契约，避免 UI、数据库或平台实现反向塑造长期档案格式。
