namespace EzNutrition.Archives.Contracts.Validation;

/// <summary>
/// 提供档案语义校验器返回的稳定问题代码。
/// </summary>
public static class ArchiveValidationCodes
{
    /// <summary>资源时间顺序无效。</summary>
    public const string InvalidResourceTimeline = "archive.resource.timeline-invalid";

    /// <summary>资源生命周期字段与状态不一致。</summary>
    public const string InvalidLifecycleState = "archive.resource.lifecycle-invalid";

    /// <summary>正式资源缺少确认信息。</summary>
    public const string MissingFinalization = "archive.resource.finalization-missing";

    /// <summary>修订关系无效。</summary>
    public const string InvalidRevisionRelationship = "archive.resource.revision-invalid";

    /// <summary>扩展同时包含原子值和子扩展。</summary>
    public const string ExtensionChoiceConflict = "archive.extension.choice-conflict";

    /// <summary>扩展缺少值和子扩展。</summary>
    public const string EmptyExtension = "archive.extension.empty";

    /// <summary>扩展嵌套超过语义校验限制。</summary>
    public const string ExtensionDepthExceeded = "archive.extension.depth-exceeded";

    /// <summary>Bundle 包含重复的资源版本。</summary>
    public const string DuplicateResourceVersion = "archive.bundle.resource-version-duplicate";

    /// <summary>资源引用无法在当前 Bundle 中解析。</summary>
    public const string UnresolvedReference = "archive.bundle.reference-unresolved";

    /// <summary>资源引用声明的类型与实际资源类型不一致。</summary>
    public const string ReferenceTypeMismatch = "archive.bundle.reference-type-mismatch";

    /// <summary>咨询文档的引用闭包不完整。</summary>
    public const string ConsultationClosureMismatch = "archive.consultation.closure-mismatch";

    /// <summary>同一咨询中的对象引用不一致。</summary>
    public const string SubjectReferenceMismatch = "archive.consultation.subject-mismatch";

    /// <summary>同一资源中出现重复的局部标识或编码。</summary>
    public const string DuplicateLocalIdentity = "archive.resource.local-identity-duplicate";

    /// <summary>必需的专业决定、状态或来源尚未建立。</summary>
    public const string RequiredSemanticValueMissing = "archive.resource.semantic-value-missing";

    /// <summary>值与其明确缺失原因同时存在。</summary>
    public const string ValueAndAbsentReasonConflict = "archive.resource.value-absent-reason-conflict";

    /// <summary>基础值与采用值的差异缺少说明。</summary>
    public const string AdjustmentReasonMissing = "archive.assessment.adjustment-reason-missing";

    /// <summary>参考数据身份或内容指纹说明不完整。</summary>
    public const string ReferenceDataIdentityIncomplete = "archive.reference-data.identity-incomplete";

    /// <summary>营养素或能量汇总无法与组成项复核。</summary>
    public const string NutrientAggregationMismatch = "archive.nutrition.aggregation-mismatch";

    /// <summary>宏量营养素折算能量超出明确容差。</summary>
    public const string EnergyConsistencyExceeded = "archive.nutrition.energy-consistency-exceeded";

    /// <summary>数量单位不一致，无法安全比较。</summary>
    public const string IncompatibleUnits = "archive.quantity.unit-mismatch";

    /// <summary>比例、顺序或其他技术数值超出定义范围。</summary>
    public const string InvalidTechnicalValue = "archive.resource.technical-value-invalid";

    /// <summary>数值需要专业人员留意，但契约允许保存。</summary>
    public const string ClinicalValueReview = "archive.clinical.value-review";

    /// <summary>资源历史形成多个未解决的当前版本头。</summary>
    public const string ConcurrentVersionHeads = "archive.resource.concurrent-heads";

    /// <summary>营养建议生成状态与时间或内容不一致。</summary>
    public const string AdviceGenerationStateInvalid = "archive.advice.generation-state-invalid";
}
