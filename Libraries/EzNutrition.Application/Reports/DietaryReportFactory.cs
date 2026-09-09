using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Application.Archives;
using EzNutrition.Application.Consultations;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Domain.Dietary;

namespace EzNutrition.Application.Reports;

/// <summary>将已完成核算的膳食调查捕获为可审核报告。</summary>
public sealed class DietaryReportFactory(ArchiveContractAssembler assembler)
{
    /// <summary>捕获本次膳食记录、结果和参考资料。</summary>
    public DietaryReportDraft Create(ConsultationWorkspace workspace, CanonicalReference template,
        ActorReference? signer, DateTimeOffset capturedAt, SignedReport? previous = null, DietaryReportOptions? options = null)
    {
        RequireCalculated(workspace);
        if (string.IsNullOrWhiteSpace(template.Version)) throw new ArgumentException("报告模板必须声明版本。", nameof(template));
        if (signer is not null && (string.IsNullOrWhiteSpace(workspace.Client.Name)
            || signer.Identifier is null || string.IsNullOrWhiteSpace(signer.Display) || signer.AbsentReason is not null))
            throw new InvalidOperationException("正式报告需要患者姓名及完整的签发人身份。");
        var document = assembler.CreateDietaryDocument(workspace, capturedAt);
        return new DietaryReportDraft(ReportDraftAssembler.Create(document, ArchiveResourceTypes.DietaryRecall,
            "24 小时膳食调查报告", template, signer, capturedAt, previous), signer, previous, workspace, options ?? new());
    }

    /// <summary>要求现有核算结果仍与当前录入一致，记录编辑后须重新核算。</summary>
    public static void RequireCalculated(ConsultationWorkspace workspace)
    {
        var survey = workspace.DietaryRecallSurvey;
        if (survey?.SummaryCalculationTable is null || survey.RecallEntries.Count == 0
            || survey.EntryCalculations.Count != survey.RecallEntries.Count || survey.NutrientAssessments.Count == 0)
            throw new InvalidOperationException("请先录入食物并完成膳食核算，再生成报告。");
        for (var i = 0; i < survey.RecallEntries.Count; i++)
        {
            var entry = survey.RecallEntries[i];
            var result = survey.EntryCalculations[i];
            var edible = entry.IsAllEdible ? entry.Weight : entry.Weight * (entry.Food.EdiblePortion ?? 100) / 100m;
            if (entry.EntryId != result.EntryId || entry.Food.FoodId != result.FoodId
                || entry.Weight <= 0 || entry.Weight != result.RecordedWeight || edible != result.EdibleWeight
                || entry.MealOccasion != result.MealOccasion || entry.IsAllEdible != result.IsAllEdible
                || entry.Food.FriendlyName != result.FoodName)
                throw new InvalidOperationException("膳食记录已变化，请重新核算后生成报告。");
        }
    }
}

/// <summary>保存膳食报告的固定契约快照和已核算参考比较。</summary>
public sealed class DietaryReportDraft : ReportDraft
{
    private readonly ConsultationWorkspace workspace;
    private readonly DietaryRecallSurvey survey;
    private readonly SummaryCalculationTable summary;
    private readonly object patient;
    private readonly EntryState[] entries;

    internal DietaryReportDraft(ArchiveDocument document, ActorReference? signer, SignedReport? previous,
        ConsultationWorkspace workspace, DietaryReportOptions options) : base(document, signer, previous)
    {
        Options = options;
        this.workspace = workspace;
        survey = workspace.DietaryRecallSurvey!;
        summary = survey.SummaryCalculationTable!;
        patient = PatientState(workspace);
        entries = survey.RecallEntries.Select(EntryState.From).ToArray();
        NutrientAssessments = Array.AsReadOnly(survey.NutrientAssessments.Select(value => value with
        {
            ContextReferences = Array.AsReadOnly(value.ContextReferences.ToArray()),
            MealEnergies = Array.AsReadOnly(value.MealEnergies.ToArray()),
            FoodContributions = Array.AsReadOnly(value.FoodContributions.ToArray())
        }).ToArray());
    }

    /// <summary>获取本次核算的营养素比较与来源分解。</summary>
    public IReadOnlyList<DietaryNutrientAssessment> NutrientAssessments { get; }

    /// <summary>获取与本次预览固定绑定的展示配置。</summary>
    public DietaryReportOptions Options { get; }

    /// <summary>确认前核对原咨询、核算结果和录入状态。</summary>
    public void EnsureCurrent()
    {
        if (!ReferenceEquals(workspace.DietaryRecallSurvey, survey) || !ReferenceEquals(survey.SummaryCalculationTable, summary)
            || !patient.Equals(PatientState(workspace)) || !entries.SequenceEqual(survey.RecallEntries.Select(EntryState.From)))
            throw new InvalidOperationException("膳食调查或患者资料已变化，请重新生成并审核报告。");
    }

    private static object PatientState(ConsultationWorkspace workspace) =>
        (workspace.Client.Name, workspace.Client.Gender, workspace.Client.Age, workspace.Client.Height,
            workspace.Client.Weight, workspace.Client.SpecialPhysiologicalPeriod);

    private sealed record EntryState(Guid Id, Guid FoodId, decimal Weight, bool AllEdible, MealOccasion Meal)
    {
        public static EntryState From(DietaryRecallEntry entry) =>
            new(entry.EntryId, entry.Food.FoodId, entry.Weight, entry.IsAllEdible, entry.MealOccasion);
    }
}

/// <summary>由 Blazor 宿主共用的膳食报告 PDF 渲染端口。</summary>
public interface IDietaryReportRenderer
{
    /// <summary>获取模板身份和确切版本。</summary>
    CanonicalReference Template { get; }

    /// <summary>将固定快照渲染为 PDF。</summary>
    ValueTask<byte[]> RenderAsync(DietaryReportDraft draft, CancellationToken cancellationToken = default);
}
