using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Shared.Data.DietaryRecallSurvey;
using EzNutrition.Shared.Data.Entities;
using System.Security.Cryptography;
using System.Text;

namespace EzNutrition.Client.Archives;

internal static class ArchiveContractCoding
{
    private static readonly Uri Root = new("https://eznutrition.cdorey.net/archive/codes/");
    private static readonly Uri Ucum = new("http://unitsofmeasure.org");
    private static readonly Uri AdministrativeSexSystem = new(Root, "administrative-sex");
    private static readonly Uri PhysiologicalStateSystem = new(Root, "physiological-state");
    private static readonly Uri NutrientSystem = new(Root, "nutrient");
    private static readonly Uri LegacyNutrientSystem = new(Root, "legacy-nutrient-name");
    private static readonly Uri FoodSystem = new(Root, "food");
    private static readonly Uri LegacyFoodSystem = new(Root, "legacy-food-name");
    private static readonly Uri MealOccasionSystem = new(Root, "meal-occasion");
    private static readonly Uri FoodGroupSystem = new(Root, "food-group");
    private static readonly Uri LocalUnitSystem = new(Root, "unit");

    private static readonly IReadOnlyDictionary<string, string> NutrientCodes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["能量"] = "energy",
            ["蛋白质"] = "protein",
            ["脂肪"] = "total-fat",
            ["总脂肪"] = "total-fat",
            ["碳水化合物"] = "carbohydrate",
            ["钙"] = "calcium",
            ["钾"] = "potassium",
            ["钠"] = "sodium",
            ["镁"] = "magnesium",
            ["铁"] = "iron",
            ["锰"] = "manganese",
            ["锌"] = "zinc",
            ["磷"] = "phosphorus",
            ["硒"] = "selenium",
            ["铜"] = "copper",
            ["胆碱"] = "choline",
            ["VitA"] = "vitamin-a",
            ["总维生素A"] = "vitamin-a-total",
            ["视黄醇"] = "retinol",
            ["胡萝卜素"] = "carotene",
            ["维生素B1"] = "thiamin",
            ["硫胺素"] = "thiamin",
            ["维生素B2"] = "riboflavin",
            ["核黄素"] = "riboflavin",
            ["烟酸"] = "niacin",
            ["维生素C"] = "vitamin-c",
            ["总维生素E"] = "vitamin-e-total"
        };

    public static Uri CodeSystem(string relativePath) => new(Root, relativePath);

    public static Coding Code(string systemPath, string code, string? display = null, string? version = "1") =>
        new(CodeSystem(systemPath), code, version, display);

    public static Coding AdministrativeSex(string? value) => value?.Trim() switch
    {
        "男" => new Coding(AdministrativeSexSystem, "male", display: "男"),
        "女" => new Coding(AdministrativeSexSystem, "female", display: "女"),
        { Length: > 0 } other => new Coding(
            AdministrativeSexSystem,
            StableCode("other", other),
            display: other),
        _ => new Coding(AdministrativeSexSystem, "unknown", display: "未说明")
    };

    public static Coding PhysiologicalState(string value)
    {
        var normalized = value.Trim();
        var code = normalized switch
        {
            "孕早期" => "pregnancy-first-trimester",
            "孕中期" => "pregnancy-second-trimester",
            "孕晚期" => "pregnancy-third-trimester",
            "乳母" => "lactation",
            "已绝经" => "postmenopausal",
            _ => StableCode("state", normalized)
        };
        return new Coding(PhysiologicalStateSystem, code, display: normalized);
    }

    public static Coding Nutrient(string? friendlyName)
    {
        var display = string.IsNullOrWhiteSpace(friendlyName) ? "未命名营养素" : friendlyName.Trim();
        return NutrientCodes.TryGetValue(display, out var code)
            ? new Coding(NutrientSystem, code, display: display)
            : new Coding(LegacyNutrientSystem, StableCode("nutrient", display), display: display);
    }

    public static Coding Food(Food food)
    {
        var display = string.IsNullOrWhiteSpace(food.FriendlyName) ? "未命名食物" : food.FriendlyName.Trim();
        return string.IsNullOrWhiteSpace(food.FriendlyCode)
            ? new Coding(LegacyFoodSystem, StableCode("food", display), display: display)
            : new Coding(FoodSystem, food.FriendlyCode.Trim(), display: display);
    }

    public static Coding MealOccasion(MealOccasion occasion) => occasion switch
    {
        EzNutrition.Shared.Data.DietaryRecallSurvey.MealOccasion.Breakfast =>
            new Coding(MealOccasionSystem, "breakfast", display: "早餐"),
        EzNutrition.Shared.Data.DietaryRecallSurvey.MealOccasion.MorningSnack =>
            new Coding(MealOccasionSystem, "morning-snack", display: "上午加餐"),
        EzNutrition.Shared.Data.DietaryRecallSurvey.MealOccasion.Lunch =>
            new Coding(MealOccasionSystem, "lunch", display: "午餐"),
        EzNutrition.Shared.Data.DietaryRecallSurvey.MealOccasion.AfternoonSnack =>
            new Coding(MealOccasionSystem, "afternoon-snack", display: "下午加餐"),
        EzNutrition.Shared.Data.DietaryRecallSurvey.MealOccasion.Dinner =>
            new Coding(MealOccasionSystem, "dinner", display: "晚餐"),
        EzNutrition.Shared.Data.DietaryRecallSurvey.MealOccasion.LateNightSnack =>
            new Coding(MealOccasionSystem, "late-night-snack", display: "宵夜"),
        _ => new Coding(MealOccasionSystem, StableCode("meal", occasion.ToString()), display: occasion.ToString())
    };

    public static Coding FoodGroup(string value)
    {
        var display = value.Trim();
        return new Coding(FoodGroupSystem, StableCode("group", display), display: display);
    }

    public static Coding Unit(string? value)
    {
        var display = string.IsNullOrWhiteSpace(value) ? "未说明单位" : value.Trim();
        var normalized = display.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        var ucumCode = normalized switch
        {
            "g" or "克" => "g",
            "g/d" or "克/日" => "g/d",
            "mg" or "毫克" => "mg",
            "mg/d" or "毫克/日" => "mg/d",
            "μg" or "ug" or "微克" => "ug",
            "μg/d" or "ug/d" or "微克/日" => "ug/d",
            "kg" or "千克" => "kg",
            "cm" or "厘米" => "cm",
            "kcal" or "千卡" => "kcal",
            "kcal/d" or "千卡/日" => "kcal/d",
            "a" or "年" => "a",
            _ => null
        };

        return ucumCode is null
            ? new Coding(LocalUnitSystem, StableCode("unit", display), display: display)
            : new Coding(Ucum, ucumCode, display: display);
    }

    public static Quantity Quantity(decimal value, string? unit) => new(value, Unit(unit));

    public static ReferenceDataIdentity EerReferenceData() => ReferenceData("eer-dataset");

    public static ReferenceDataIdentity DriReferenceData() => ReferenceData("dri-dataset");

    public static ReferenceDataIdentity FoodCompositionReferenceData() =>
        ReferenceData("food-composition-dataset");

    public static ReferenceDataIdentity DietaryGuidelineReferenceData() =>
        ReferenceData("dietary-guideline-pagoda");

    public static string StableCode(string prefix, string source)
    {
        var normalized = source.Trim().ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return $"{prefix}-{Convert.ToHexString(hash.AsSpan(0, 12)).ToLowerInvariant()}";
    }

    private static ReferenceDataIdentity ReferenceData(string code) => new(CodeSystem("reference-data"), code)
    {
        FingerprintAbsentReason = DataAbsentReasonCode.NotEstablished
    };
}
