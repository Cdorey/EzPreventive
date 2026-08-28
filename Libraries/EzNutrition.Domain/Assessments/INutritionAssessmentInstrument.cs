namespace EzNutrition.Domain.Assessments;

/// <summary>
/// 表示一个可由工作台发现、作答并确定性计分的营养筛查或评估量表。
/// </summary>
/// <remarks>
/// 具体量表实现应只包含量表定义与领域规则，不应依赖 Razor、依赖注入容器或档案序列化格式。
/// </remarks>
public interface INutritionAssessmentInstrument
{
    /// <summary>
    /// 获取当前实现所对应的不可变量表定义。
    /// </summary>
    NutritionAssessmentDefinition Definition { get; }

    /// <summary>
    /// 根据当前回答和评估对象快照形成量表状态、分值与解释。
    /// </summary>
    /// <param name="answers">按稳定题目编码保存的当前回答。</param>
    /// <param name="subject">开始本次量表时取得的评估对象快照。</param>
    /// <returns>当前回答对应的完整或未完成评估结果。</returns>
    NutritionAssessmentEvaluation Evaluate(
        IReadOnlyDictionary<string, string> answers,
        NutritionAssessmentSubject subject);
}
