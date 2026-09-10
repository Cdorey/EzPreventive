using System.Globalization;
using EzNutrition.Application.Reports;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.ValueObjects;
using Microsoft.JSInterop;

namespace EzNutrition.Presentation.Reports;

/// <summary>将能量核算固定快照排版为本机 PDF。</summary>
public sealed class PdfMakeEnergyReportRenderer(IJSRuntime js) : IEnergyReportRenderer
{
    /// <inheritdoc />
    public CanonicalReference Template { get; } = new(new Uri("https://eznutrition.cdorey.net/report-templates/energy-assessment"), "1");

    /// <inheritdoc />
    public async ValueTask<byte[]> RenderAsync(EnergyReportDraft draft, CancellationToken cancellationToken = default)
    {
        if (draft.Report.PresentationTemplate != Template)
            throw new InvalidOperationException("能量报告模板版本已变化，请重新生成预览。");
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", cancellationToken,
            "./_content/EzNutrition.Presentation/reports/energy-report.mjs");
        var pdf = await module.InvokeAsync<byte[]>("render", cancellationToken, EnergyReportPdfModel.From(draft));
        _ = ReportPdf.Identity(pdf);
        return pdf;
    }
}

/// <summary>向模板提供已经核算、格式化的能量报告内容。</summary>
internal sealed record EnergyReportPdfModel
{
    public required string Title { get; init; }
    public required string ReportNumber { get; init; }
    public required int RevisionNumber { get; init; }
    public required string Patient { get; init; }
    public required string Subject { get; init; }
    public required string ReportTime { get; init; }
    public required string Signer { get; init; }
    public required string Institution { get; init; }
    public required bool IsEvaluation { get; init; }
    public required EnergyReportOptions Options { get; init; }
    public required string EnergyTarget { get; init; }
    public required string[][] TotalEnergy { get; init; }
    public required string[][] Macronutrients { get; init; }
    public required string[][] Meals { get; init; }
    public required string[][] MealExchanges { get; init; }
    public required string[][] FoodExchanges { get; init; }

    public static EnergyReportPdfModel From(EnergyReportDraft draft)
    {
        var energy = draft.Document.Bundle.Entries.OfType<EnergyAssessmentResource>().Single();
        var subject = draft.Document.Bundle.Entries.OfType<ConsultationResource>().Single().SubjectSnapshot!;
        var plan = energy.AllocationPlan!;
        List<string[]> totals =
        [
            ["采用总能量", Quantity(energy.ProfessionalDecision!.AdoptedEnergyTarget)],
            ["采用依据", energy.ProfessionalDecision.DecisionBasis?.Display ?? "未记录"]
        ];
        foreach (var candidate in energy.CandidateCalculations)
        {
            totals.Add(["程序估算总能量", Quantity(candidate.Result)]);
            totals.Add(["计算方法", candidate.Algorithm.Method.Code switch
            {
                "ideal-body-weight-bee-pal" => "身高推导理想体重 × 基础能量系数（BEE）× 身体活动水平（PAL）",
                "population-average-eer" => "参考人群平均体重对应的估计能量需要量（EER）",
                _ => candidate.Algorithm.Method.Display ?? candidate.Algorithm.Method.Code
            }]);
            totals.AddRange(candidate.Inputs.Where(input => input.Parameter.Code is "physical-activity-level" or "basal-energy-coefficient")
                .Select(input => new[] { input.Parameter.Display ?? input.Parameter.Code, Value(input.AdoptedValue) }));
            totals.AddRange(candidate.IntermediateResults.Select(result => new[]
            {
                result.Name.Code == "physiological-energy-offset" ? "程序估算已计入的生理阶段附加量" : result.Name.Display ?? result.Name.Code,
                Value(result.Value)
            }));
        }
        var nutrients = plan.MacronutrientTargets;
        return new EnergyReportPdfModel
        {
            Title = draft.Report.Title!, ReportNumber = draft.Report.Metadata.ResourceId.Value.ToString("D"),
            RevisionNumber = draft.Report.Metadata.RevisionNumber.Value, Patient = subject.IdentityDisplay ?? "未关联患者",
            Subject = $"{subject.AdministrativeSex?.Display ?? "性别未提供"} · {subject.ChronologicalAgeAtConsultation?.ToString() ?? "年龄未提供"}"
                + $" · 身高 {Quantity(subject.Height?.Value)} · 体重 {Quantity(subject.Weight?.Value)}"
                + (subject.PhysiologicalStates.Count > 0 ? " · " + string.Join("、", subject.PhysiologicalStates.Select(value => value.Display ?? value.Code)) : ""),
            ReportTime = draft.Report.Metadata.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            Signer = draft.Signer?.Display ?? "未经医师审核签发", Institution = draft.Signer?.Organization?.Display ?? "",
            IsEvaluation = draft.Signer is null, Options = draft.Options, EnergyTarget = Quantity(plan.EnergyTarget), TotalEnergy = totals.ToArray(),
            Macronutrients = nutrients.Select(nutrient => new[] { NutrientName(nutrient.Nutrient), Number(nutrient.EnergyFraction * 100) + "%", Quantity(nutrient.DailyAmount) }).ToArray(),
            Meals = new[] { "早餐", "午餐", "晚餐" }.Select(meal => new[] { meal }.Concat(nutrients.Select(nutrient =>
                Quantity(nutrient.MealAllocations.Single(value => value.MealOccasion.Display == meal).Amount))).ToArray()).ToArray(),
            MealExchanges = draft.MealExchanges.Select(meal => new[] { meal.Meal, Number((decimal)meal.Protein),
                Number((decimal)meal.Carbohydrate), Number((decimal)meal.Fat) }).ToArray(),
            FoodExchanges = plan.FoodExchangeTargets.Select(target => new[] { target.FoodGroup.Display ?? target.FoodGroup.Code,
                Number(target.DailyExchanges.Value) }).ToArray()
        };
    }

    private static string NutrientName(Coding nutrient) => nutrient.Display == "总脂肪" ? "脂肪" : nutrient.Display ?? nutrient.Code;
    private static string Quantity(Quantity? value) => value is null ? "未记录" : Number(value.Value) + " " + (value.Unit.Code switch
    {
        "kcal/d" => "kcal/日", "g/d" => "g/日", _ => value.Unit.Display ?? value.Unit.Code
    });
    private static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Value(ArchiveValue value) => value switch
    {
        QuantityArchiveValue quantity => Quantity(quantity.Value), DecimalArchiveValue number => Number(number.Value),
        TextArchiveValue text => text.Value, CodingArchiveValue coding => coding.Value.Display ?? coding.Value.Code,
        _ => "未记录"
    };
}
