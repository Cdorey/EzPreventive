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
            LowerReference = vitaminAReference?.RNI?.Value
        });
        assessments.Add(new DietaryNutrientAssessment
        {
            FriendlyName = "视黄醇",
            Value = calculation["视黄醇"],
            Unit = vitaminAReference?.UL?.MeasureUnit ?? string.Empty,
            UpperReference = vitaminAReference?.UL?.Value
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
            LowerReference = reference?.RNI?.Value,
            UpperReference = reference?.UL?.Value,
            FoodContributions = contributions
                .Select(value => new DietaryFoodContribution(
                    value.Food?.FriendlyName ?? string.Empty,
                    value.Value,
                    "g"))
                .ToArray()
        });

        assessments.Add(new DietaryNutrientAssessment
        {
            FriendlyName = ratioName,
            Value = PercentageOfTotalEnergy(componentEnergy, calculation.TotalEnergy),
            Unit = "%E",
            LowerReference = reference?.OtherRecords
                .FirstOrDefault(value => value.RecordType == DietaryReferenceIntakeType.AMDR_L)?.Value,
            UpperReference = reference?.OtherRecords
                .FirstOrDefault(value => value.RecordType == DietaryReferenceIntakeType.AMDR_H)?.Value
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

        return new DietaryNutrientAssessment
        {
            Abbreviation = abbreviation ?? string.Empty,
            FriendlyName = friendlyName,
            Value = calculation[compositionName ?? friendlyName],
            Unit = reference?.RNI?.MeasureUnit ?? string.Empty,
            LowerReference = reference?.RNI?.Value,
            UpperReference = reference?.UL?.Value
        };
    }

    private static DietaryMealEnergy CreateMealEnergy(
        SummaryCalculationTable calculation,
        MealOccasion occasion)
    {
        var energy = calculation[occasion]
            .FirstOrDefault(value => value.Nutrient?.FriendlyName == "能量")?.Value ?? 0m;
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

    public void Calculate()
    {
        var calculation = new SummaryCalculationTable(RecallEntries, Nutrients.ToList());
        var assessments = CreateNutrientAssessments(calculation);
        var entryCalculations = calculation.CreateEntryCalculations();

        NutrientAssessments.Clear();
        NutrientAssessments.AddRange(assessments);
        EntryCalculations = entryCalculations;
        SummaryCalculationTable = calculation;
        OnCalculate?.Invoke(this, EventArgs.Empty);
    }

    public void ResetCalculation()
    {
        SummaryCalculationTable = null;
        NutrientAssessments.Clear();
        EntryCalculations = [];
    }

}
