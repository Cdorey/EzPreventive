using EzNutrition.Application.Archives;
using EzNutrition.Application.Consultations;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Metadata;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Application.Reports;

/// <summary>从当前咨询捕获单份量表报告，复用已有档案映射，不在模板里重新解释量表规则。</summary>
public sealed class AssessmentReportFactory(ArchiveContractAssembler assembler)
{
    /// <summary>
    /// 创建独立预览；正式签发需完整量表、明确患者及具有稳定身份和姓名的签发人。
    /// </summary>
    /// <param name="workspace">当前咨询。</param>
    /// <param name="assessment">要输出的量表。</param>
    /// <param name="template">渲染器提供的模板标识与版本。</param>
    /// <param name="signer">通过现有权限检查后取得的拟签发人；评估输出传空。</param>
    /// <param name="capturedAt">预览中显示的报告时间。</param>
    /// <param name="previous">同一量表报告的当前正式版本；为空时建立初版。</param>
    public AssessmentReportDraft Create(
        ConsultationWorkspace workspace,
        NutritionAssessmentRun assessment,
        CanonicalReference template,
        ActorReference? signer,
        DateTimeOffset capturedAt,
        SignedReport? previous = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(assessment);
        ArgumentNullException.ThrowIfNull(template);
        if (string.IsNullOrWhiteSpace(template.Version))
        {
            throw new ArgumentException("报告模板必须声明确切版本。", nameof(template));
        }

        if (signer is not null)
        {
            if (!assessment.Evaluation.IsComplete)
            {
                throw new InvalidOperationException("请完成量表后再签发报告。");
            }

            if (string.IsNullOrWhiteSpace(workspace.Client.Name))
            {
                throw new InvalidOperationException("正式报告需要明确的患者姓名。");
            }

            if (signer.Identifier is null || string.IsNullOrWhiteSpace(signer.Display) || signer.AbsentReason is not null)
            {
                throw new InvalidOperationException("签发人的稳定身份或姓名不完整。");
            }
        }

        var document = assembler.CreateAssessmentDocument(workspace, assessment, capturedAt);
        return new AssessmentReportDraft(ReportDraftAssembler.Create(document,
            ArchiveResourceTypes.NutritionScaleAssessment, $"{assessment.Definition.DisplayName}报告",
            template, signer, capturedAt, previous), signer, previous);
    }
}
