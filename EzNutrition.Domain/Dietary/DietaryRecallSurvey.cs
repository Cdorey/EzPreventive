using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;
using EzNutrition.Shared.Data.Entities;
using System.Text.Json.Serialization;

namespace EzNutrition.Domain.Dietary;

public class DietaryRecallSurvey(
    IClient client,
    IEnumerable<Food> foods,
    IEnumerable<Nutrient> nutrients,
    DRIs dRIs) : ITreatment
{
    private static readonly MealOccasion[] MealOccasions =
    [
        MealOccasion.Breakfast,
        MealOccasion.MorningSnack,
        MealOccasion.Lunch,
        MealOccasion.AfternoonSnack,
        MealOccasion.Dinner,
        MealOccasion.LateNightSnack
    ];

    private IReadOnlyList<DietaryNutrientAssessment> CreateNutrientAssessments(
        SummaryCalculationTable calculation)
    {
        var assessments = new List<DietaryNutrientAssessment>
        {
            new()
            {
                Abbreviation = "E",
                FriendlyName = "总能量",
                Value = calculation.TotalEnergy,
                Unit = "kCal",
                MealEnergies = MealOccasions
                    .Select(occasion => CreateMealEnergy(calculation, occasion))
                    .ToArray()
            }
        };

        AddMacronutrientAssessments(
            assessments,
            calculation,
            assessmentName: "蛋白质",
            compositionName: "蛋白质",
            driName: "蛋白质",
            ratioName: "蛋白质供能比",
            componentEnergy: calculation.ProteinEnergy,
            contributions: calculation.ProteinRank);
        AddMacronutrientAssessments(
            assessments,
            calculation,
            assessmentName: "总脂肪",
            compositionName: "脂肪",
            driName: "总脂肪",
            ratioName: "脂肪供能比",
            componentEnergy: calculation.FatEnergy,
            contributions: calculation.FatRank);
        AddMacronutrientAssessments(
            assessments,
            calculation,
            assessmentName: "碳水化合物",
            compositionName: "碳水化合物",
            driName: "碳水化合物",
            ratioName: "碳水化合物供能比",
            componentEnergy: calculation.CarbohydrateEnergy,
            contributions: calculation.CarbohydrateRank);

        assessments.Add(CreateNutrientAssessment(calculation, "钾", "K"));
        assessments.Add(CreateNutrientAssessment(calculation, "钠", "Na"));
        assessments.Add(CreateNutrientAssessment(calculation, "镁", "Mg"));
        assessments.Add(CreateNutrientAssessment(calculation, "铁", "Fe"));
        assessments.Add(CreateNutrientAssessment(calculation, "锰", "Mn"));
        assessments.Add(CreateNutrientAssessment(calculation, "锌", "Zn"));
        assessments.Add(CreateNutrientAssessment(calculation, "磷", "P"));
        assessments.Add(CreateNutrientAssessment(calculation, "硒", "Se"));
        assessments.Add(CreateNutrientAssessment(calculation, "铜", "Cu"));

        var vitaminAReference = DRIs.NutrientRanges.FirstOrDefault(range => range.Nutrient == "VitA");
        assessments.Add(new DietaryNutrientAssessment
        {
            Abbreviation = "VitA",
            FriendlyName = "总维生素A",
            Value = calculation["总维生素A"],
            Unit = vitaminAReference?.RNI?.MeasureUnit ?? string.Empty,
            LowerReference = CreateReference(vitaminAReference?.RNI),
            ContextReferences = CreateContextReferences(
                vitaminAReference,
                vitaminAReference?.RNI?.MeasureUnit)
        });
        assessments.Add(new DietaryNutrientAssessment
        {
            FriendlyName = "视黄醇",
            Value = calculation["视黄醇"],
            Unit = vitaminAReference?.UL?.MeasureUnit ?? string.Empty,
            UpperReference = CreateReference(vitaminAReference?.UL)
        });
        assessments.Add(CreateNutrientAssessment(calculation, "胡萝卜素"));
        assessments.Add(CreateNutrientAssessment(calculation, "维生素B1", "VitB1", "硫胺素", "VitB1"));
        assessments.Add(CreateNutrientAssessment(calculation, "维生素B2", "VitB2", "核黄素", "VitB2"));
        assessments.Add(CreateNutrientAssessment(calculation, "烟酸", "VitB3", driName: "VitB3") with
        {
            Unit = "mg"
        });
        assessments.Add(CreateNutrientAssessment(calculation, "维生素C", "VitC", driName: "VitC"));
        assessments.Add(CreateNutrientAssessment(calculation, "总维生素E", "VitE", driName: "VitE"));

        return assessments;
    }

    private void AddMacronutrientAssessments(
        ICollection<DietaryNutrientAssessment> assessments,
        SummaryCalculationTable calculation,
        string assessmentName,
        string compositionName,
        string driName,
        string ratioName,
        decimal componentEnergy,
        IEnumerable<FoodNutrientValue> contributions)
    {
        var reference = DRIs.NutrientRanges.FirstOrDefault(range => range.Nutrient == driName);
        assessments.Add(new DietaryNutrientAssessment
        {
            FriendlyName = assessmentName,
            Value = calculation[compositionName],
            Unit = "g",
            LowerReference = CreateReference(reference?.RNI),
            UpperReference = CreateReference(reference?.UL),
            ContextReferences = CreateContextReferences(reference, "g"),
            FoodContributions = contributions
                .Select(value => new DietaryFoodContribution(
                    value.Food?.FriendlyName ?? string.Empty,
                    value.Value,
                    "g"))
                .ToArray()
        });

        var lowerAmdr = reference?.OtherRecords
            .FirstOrDefault(value => value.RecordType == DietaryReferenceIntakeType.AMDR_L);
        var upperAmdr = reference?.OtherRecords
            .FirstOrDefault(value => value.RecordType == DietaryReferenceIntakeType.AMDR_H);
        assessments.Add(new DietaryNutrientAssessment
        {
            FriendlyName = ratioName,
            Value = PercentageOfTotalEnergy(componentEnergy, calculation.TotalEnergy),
            Unit = "%E",
            LowerReference = CreateReference(lowerAmdr),
            UpperReference = CreateReference(upperAmdr),
            ContextReferences = CreateContextReferences(reference, "%E")
        });
    }

    private DietaryNutrientAssessment CreateNutrientAssessment(
        SummaryCalculationTable calculation,
        string friendlyName,
        string? abbreviation = null,
        string? compositionName = null,
        string? driName = null)
    {
        var reference = DRIs.NutrientRanges.FirstOrDefault(
            range => range.Nutrient == (driName ?? friendlyName));

        var unit = reference?.RNI?.MeasureUnit
            ?? reference?.UL?.MeasureUnit
            ?? string.Empty;
        return new DietaryNutrientAssessment
        {
            Abbreviation = abbreviation ?? string.Empty,
            FriendlyName = friendlyName,
            Value = calculation[compositionName ?? friendlyName],
            Unit = unit,
            LowerReference = CreateReference(reference?.RNI),
            UpperReference = CreateReference(reference?.UL),
            ContextReferences = CreateContextReferences(reference, unit)
        };
    }

    private static IReadOnlyList<DietaryNutrientReference> CreateContextReferences(
        NutrientRange? range,
        string? assessmentUnit)
    {
        var reference = CreateReference(range?.PiNcd);
        return reference is not null && UnitsMatch(reference.Unit, assessmentUnit)
            ? [reference]
            : [];
    }

    private static bool UnitsMatch(string referenceUnit, string? assessmentUnit) =>
        string.Equals(
            NormalizeDailyUnit(referenceUnit),
            NormalizeDailyUnit(assessmentUnit),
            StringComparison.OrdinalIgnoreCase);

    private static string NormalizeDailyUnit(string? unit) =>
        (unit ?? string.Empty).Trim().Replace("/d", string.Empty, StringComparison.OrdinalIgnoreCase);

    private static DietaryNutrientReference? CreateReference(AggregatedDriValue? reference) =>
        reference?.ResolvedValue is { } value
            ? new DietaryNutrientReference(
                reference.RecordType,
                value,
                reference.MeasureUnit ?? string.Empty)
            : null;

    private static DietaryNutrientReference? CreateReference(
        DietaryReferenceIntakeValue? reference) =>
        reference is null
            ? null
            : new DietaryNutrientReference(
                reference.RecordType,
                reference.Value,
                reference.MeasureUnit ?? string.Empty);

    private static DietaryMealEnergy CreateMealEnergy(
        SummaryCalculationTable calculation,
        MealOccasion occasion)
    {
        var energy = calculation.GetValue(occasion, "能量");
        return new DietaryMealEnergy(
            occasion,
            energy,
            PercentageOfTotalEnergy(energy, calculation.TotalEnergy));
    }

    private static decimal PercentageOfTotalEnergy(decimal componentEnergy, decimal totalEnergy) =>
        totalEnergy == 0m
            ? 0m
            : Math.Round(componentEnergy / totalEnergy * 100m, 0);

    public event EventHandler<EventArgs>? OnCalculate;

    [JsonIgnore]
    public string[] Requirements { get; } = [];

    public IClient Client => client;

    [JsonIgnore]
    public IEnumerable<Food> Foods => foods;

    [JsonIgnore]
    public IEnumerable<Nutrient> Nutrients => nutrients;

    [JsonIgnore]
    public DRIs DRIs => dRIs;

    public List<DietaryRecallEntry> RecallEntries { get; } = [];

    public SummaryCalculationTable? SummaryCalculationTable { get; set; }

    public List<DietaryNutrientAssessment> NutrientAssessments { get; } = [];

    public IReadOnlyList<DietaryRecallEntryCalculation> EntryCalculations { get; private set; } = [];

    /// <summary>
    /// 使用当前膳食记录完成核算并立即应用结果。
    /// </summary>
    public void Calculate()
    {
        ApplyCalculation(CreateCalculation(RecallEntries));
    }

    /// <summary>
    /// 使用给定的稳定记录快照完成核算，但不改变当前调查对象。
    /// </summary>
    /// <param name="recallEntries">按调查录入顺序排列的膳食记录快照。</param>
    /// <returns>可在核算完成后原子应用的完整结果。</returns>
    public DietaryRecallCalculationResult CreateCalculation(
        IReadOnlyList<DietaryRecallEntry> recallEntries)
    {
        ArgumentNullException.ThrowIfNull(recallEntries);

        var calculation = new SummaryCalculationTable(recallEntries.ToList(), Nutrients.ToList());
        var assessments = CreateNutrientAssessments(calculation);
        var entryCalculations = calculation.CreateEntryCalculations();

        return new DietaryRecallCalculationResult
        {
            Summary = calculation,
            NutrientAssessments = assessments,
            EntryCalculations = entryCalculations
        };
    }

    /// <summary>
    /// 一次性应用已经完成的膳食核算结果，并通知依赖该结果的领域投影。
    /// </summary>
    /// <param name="result">待应用的完整核算结果。</param>
    public void ApplyCalculation(DietaryRecallCalculationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        NutrientAssessments.Clear();
        NutrientAssessments.AddRange(result.NutrientAssessments);
        EntryCalculations = result.EntryCalculations;
        SummaryCalculationTable = result.Summary;
        OnCalculate?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 清除最近一次核算结果，使调查对象重新进入记录编辑状态。
    /// </summary>
    public void ResetCalculation()
    {
        SummaryCalculationTable = null;
        NutrientAssessments.Clear();
        EntryCalculations = [];
    }

}
