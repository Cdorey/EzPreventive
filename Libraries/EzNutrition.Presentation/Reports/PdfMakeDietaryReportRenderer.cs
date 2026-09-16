using System.Globalization;
using EzNutrition.Application.Reports;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Domain.Dietary;
using Microsoft.JSInterop;

namespace EzNutrition.Presentation.Reports;

/// <summary>将膳食调查固定快照排版为本机 PDF。</summary>
public sealed class PdfMakeDietaryReportRenderer(IJSRuntime js) : IDietaryReportRenderer
{
    /// <inheritdoc />
    public CanonicalReference Template { get; } = new(new Uri("https://eznutrition.cdorey.net/report-templates/dietary-recall"), "4");

    /// <inheritdoc />
    public async ValueTask<byte[]> RenderAsync(DietaryReportDraft draft, CancellationToken cancellationToken = default)
    {
        if (draft.Report.PresentationTemplate != Template)
            throw new InvalidOperationException("膳食报告模板版本已变化，请重新生成预览。");
        var model = DietaryReportPdfModel.From(draft);
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", cancellationToken,
            "./_content/EzNutrition.Presentation/reports/dietary-report.mjs");
        var pdf = await module.InvokeAsync<byte[]>("render", cancellationToken, model);
        _ = ReportPdf.Identity(pdf);
        return pdf;
    }
}

/// <summary>保存一种宏量营养素的食材贡献排名表。</summary>
internal sealed record DietaryReportContributionTable(string Title, string[][] Rows);

/// <summary>向模板提供已格式化的膳食记录和已有核算结果。</summary>
internal sealed record DietaryReportPdfModel
{
    public required string Title { get; init; }
    public required string ReportNumber { get; init; }
    public required int RevisionNumber { get; init; }
    public required string Patient { get; init; }
    public required string Subject { get; init; }
    public required string RecallPeriod { get; init; }
    public required string ReportTime { get; init; }
    public required string Signer { get; init; }
    public required string Institution { get; init; }
    public required bool IsEvaluation { get; init; }
    public required string[][] Foods { get; init; }
    public required string[][] Meals { get; init; }
    public required IReadOnlyList<DietaryReportNutrientGroup> NutrientGroups { get; init; }
    public required IReadOnlyList<DietaryReportContributionTable> Contributions { get; init; }
    public required string ContributionScope { get; init; }
    public required bool IncludeDriReferences { get; init; }
    public required string[][] Guidance { get; init; }
    public required string[][] References { get; init; }
    public required string[] Sources { get; init; }

    public static DietaryReportPdfModel From(DietaryReportDraft draft)
    {
        var recall = draft.Document.Bundle.Entries.OfType<DietaryRecallResource>().Single();
        var consultation = draft.Document.Bundle.Entries.OfType<ConsultationResource>().Single();
        var dri = draft.Document.Bundle.Entries.OfType<DriAssessmentResource>().Single();
        var subject = consultation.SubjectSnapshot!;
        var source = recall.Meals.SelectMany(meal => meal.Entries).Select(entry => entry.FoodCompositionData)
            .Append(dri.ReferenceData).Append(recall.GuidanceSnapshot?.Guideline).Where(value => value is not null)
            .Select(value => $"{SourceName(value!.Code)} · {value.Edition ?? value.Release ?? "版本未提供"}").Distinct().ToArray();
        return new DietaryReportPdfModel
        {
            Title = draft.Report.Title!, ReportNumber = draft.Report.Metadata.ResourceId.Value.ToString("D"),
            RevisionNumber = draft.Report.Metadata.RevisionNumber.Value, Patient = subject.IdentityDisplay ?? "未关联患者",
            Subject = $"{subject.AdministrativeSex?.Display ?? "性别未提供"} · {subject.ChronologicalAgeAtConsultation?.ToString() ?? "年龄未提供"}"
                + $" · 身高 {Quantity(subject.Height?.Value)} · 体重 {Quantity(subject.Weight?.Value)}",
            RecallPeriod = recall.RecallPeriod is { } period ? $"{Time(period.Start)} 至 {(period.End is { } end ? Time(end) : "未记录")}" : "回顾起止日期未记录",
            ReportTime = Time(draft.Report.Metadata.CreatedAt), Signer = draft.Signer?.Display ?? "未经医师审核签发",
            Institution = draft.Signer?.Organization?.Display ?? "", IsEvaluation = draft.Signer is null,
            Foods = recall.Meals.OrderBy(meal => meal.Sequence).SelectMany(meal => meal.Entries.OrderBy(entry => entry.Sequence).Select(entry => new[]
            {
                meal.Occasion.Display ?? meal.Occasion.Code, entry.Food.Display ?? entry.Food.Code,
                Quantity(entry.ReportedAmount), entry.EdibleFraction is { } fraction ? Number(fraction * 100) + "%" : "未记录",
                Quantity(entry.AdoptedConsumedAmount)
            })).ToArray(),
            Meals = draft.NutrientAssessments.SelectMany(value => value.MealEnergies).Select(meal => new[]
            {
                Meal(meal.MealOccasion), Number(meal.Energy) + " kcal", Number(meal.PercentageOfTotalEnergy) + "%"
            }).ToArray(),
            NutrientGroups = DietaryReportNutrients.Create(draft.NutrientAssessments, recall.TotalNutrientSummary),
            Contributions = CreateContributionTables(draft.NutrientAssessments, draft.Options.ContributionRankLimit),
            ContributionScope = draft.Options.ContributionRankLimit is { } limit ? $"各表列示前 {limit} 位（含并列）" : "各表列示全部食材",
            IncludeDriReferences = draft.Options.IncludeDriReferences,
            Guidance = GuidanceRows(recall.GuidanceSnapshot?.Items ?? []).ToArray(),
            References = dri.NutrientResults.SelectMany(value => value.ReferenceValues.Select(reference => new[]
            {
                value.Nutrient.Display ?? value.Nutrient.Code, reference.ReferenceType.Display ?? reference.ReferenceType.Code,
                reference.AdoptedValue is QuantityArchiveValue quantity ? Quantity(quantity.Value) : "未确定，需核定",
                string.Join("；", reference.Components.Select(component =>
                    (component.IsOffset ? "偏移 " : "基础 ") + Quantity(component.Value)
                    + (string.IsNullOrWhiteSpace(component.Detail) ? "" : " · " + component.Detail)))
            })).ToArray(),
            Sources = [$"DRIs 采用人群：{dri.PopulationGroup?.AdoptedGroup.Display ?? "未提供"}", .. source]
        };
    }

    internal static IReadOnlyList<DietaryReportContributionTable> CreateContributionTables(
        IReadOnlyList<DietaryNutrientAssessment> assessments, int? rankLimit = null)
    {
        return new[] { (Name: "蛋白质", Title: "蛋白质"), (Name: "总脂肪", Title: "脂肪"),
                (Name: "碳水化合物", Title: "碳水化合物") }
            .Select(nutrient =>
            {
                var foods = assessments.FirstOrDefault(value => value.FriendlyName == nutrient.Name)?.FoodContributions
                    .OrderByDescending(food => food.Value).ToArray() ?? [];
                var rank = 0;
                var rows = foods.Select((food, index) =>
                {
                    if (index == 0 || food.Value != foods[index - 1].Value) rank = index + 1;
                    return (Rank: rank, Row: new[] { rank.ToString(CultureInfo.InvariantCulture), food.FoodName,
                        Number(food.Value) + " " + Unit(food.Unit) });
                }).TakeWhile(item => rankLimit is null || item.Rank <= rankLimit)
                    .Select(item => item.Row).ToArray();
                return new DietaryReportContributionTable(nutrient.Title, rows);
            }).ToArray();
    }

    private static IEnumerable<string[]> GuidanceRows(IEnumerable<DietaryGuidanceItem> items, string prefix = "")
    {
        foreach (var item in items)
        {
            var name = prefix + (item.Category.Display ?? item.Category.Code);
            yield return [name, item.ObservedValue is TextArchiveValue text ? text.Value : "未记录", item.Recommendation ?? "未提供"];
            foreach (var row in GuidanceRows(item.Children, name + " / ")) yield return row;
        }
    }

    private static string Quantity(Quantity? value) => value is null ? "未记录" : $"{Number(value.Value)} {Unit(value.Unit.Display ?? value.Unit.Code)}";
    private static string Unit(string unit) => string.IsNullOrWhiteSpace(unit) ? "（单位未记录）" : unit.Equals("kCal", StringComparison.OrdinalIgnoreCase) ? "kcal" : unit;
    private static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static string SourceName(string code) => code switch
    {
        "food-composition-dataset" => "食物成分资料", "dri-dataset" => "膳食营养素参考摄入量",
        "dietary-guideline-pagoda" => "膳食指南宝塔", _ => code
    };
    private static string Time(DateTimeOffset value) => value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
    private static string Meal(MealOccasion meal) => meal switch
    {
        MealOccasion.Breakfast => "早餐", MealOccasion.MorningSnack => "上午加餐", MealOccasion.Lunch => "午餐",
        MealOccasion.AfternoonSnack => "下午加餐", MealOccasion.Dinner => "晚餐", MealOccasion.LateNightSnack => "宵夜", _ => meal.ToString()
    };
}
