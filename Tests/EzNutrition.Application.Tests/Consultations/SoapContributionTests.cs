using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;
using EzNutrition.Domain.Dietary;
using EzNutrition.Shared.Data.Entities;

namespace EzNutrition.Application.Tests.Consultations;

public sealed class SoapContributionTests
{
    [Fact]
    public void EnergyProjectionOmitsMissingDataAndDescribesManualEnergyAsAdoptedValue()
    {
        var emptyProjection = new EnergyCalculator(new ClientInfo()).ToSoapContribution();

        Assert.Empty(emptyProjection.Subjective);
        Assert.Empty(emptyProjection.Objective);
        AssertNoAssessmentOrPlan(emptyProjection);

        var calculator = new EnergyCalculator(new ClientInfo
        {
            Height = 170m,
            Weight = 70m
        })
        {
            PAL = 1.5m
        };
        Assert.True(calculator.CorrectEnergy(2000));

        var projection = calculator.ToSoapContribution();

        Assert.Empty(projection.Subjective);
        Assert.Contains("身体活动水平（PAL）：1.5", projection.Objective);
        Assert.Contains("专业人员核定的每日总能量：2000 kcal", projection.Objective);
        Assert.DoesNotContain("未记录", projection.Objective);
        AssertNoAssessmentOrPlan(projection);
    }

    [Fact]
    public void DietaryRecallProjectionKeepsReportedRecordsInSAndCalculatedResultsInO()
    {
        var client = new ClientInfo();
        var energy = Nutrient(1, "能量", "kCal");
        var food = Food("米饭", energy, 116m);
        var survey = new DietaryRecallSurvey(client, [food], [energy], new DRIs(client));
        survey.RecallEntries.Add(new DietaryRecallEntry
        {
            Food = food,
            Weight = 100m,
            MealOccasion = MealOccasion.MorningSnack
        });
        survey.SummaryCalculationTable = new SummaryCalculationTable(survey.RecallEntries, [energy]);
        survey.NutrientAssessments.AddRange(
        [
            new DietaryNutrientAssessment
            {
                FriendlyName = "蛋白质",
                Value = 50m,
                Unit = "g",
                LowerReference = new DietaryNutrientReference(
                    DietaryReferenceIntakeType.RNI,
                    65m,
                    "g/d")
            },
            new DietaryNutrientAssessment
            {
                FriendlyName = "脂肪供能比",
                Value = 40m,
                Unit = "%E",
                UpperReference = new DietaryNutrientReference(
                    DietaryReferenceIntakeType.AMDR_H,
                    35m,
                    "%E")
            }
        ]);

        var projection = survey.ToSoapContribution();

        Assert.Contains("上午加餐：米饭，记录重量 100 g", projection.Subjective);
        Assert.DoesNotContain(nameof(MealOccasion.MorningSnack), projection.Subjective);
        Assert.DoesNotContain("低于", projection.Subjective);
        Assert.DoesNotContain("高于", projection.Subjective);
        Assert.Contains("本次回顾日总能量摄入量：116 kcal", projection.Objective);
        Assert.Contains("低于推荐摄入量（RNI） 65 g/d", projection.Objective);
        Assert.Contains("高于可接受宏量营养素分布范围上限（AMDR） 35 %E", projection.Objective);
        Assert.DoesNotContain("可耐受最高摄入量（UL）", projection.Objective);
        Assert.Contains("不代表通常摄入水平", projection.Objective);
        AssertNoAssessmentOrPlan(projection);
    }

    [Fact]
    public void EmptyDietaryRecallProjectionDoesNotInventZeroIntake()
    {
        var client = new ClientInfo();
        var projection = new DietaryRecallSurvey(client, [], [], new DRIs(client))
            .ToSoapContribution();

        Assert.Empty(projection.Subjective);
        Assert.Empty(projection.Objective);
        AssertNoAssessmentOrPlan(projection);
    }

    [Fact]
    public void DietaryTowerProjectionIncludesNestedRecommendationsAndUsesRecallWording()
    {
        var standard = StandardTower.GetStandardTower(18m)!;
        var emptyProjection = new DietaryRecallTower(standard).ToSoapContribution();
        Assert.Empty(emptyProjection.Objective);
        AssertNoAssessmentOrPlan(emptyProjection);

        var food = new Food
        {
            FoodId = Guid.NewGuid(),
            FriendlyCode = "TEST-ANIMAL",
            FriendlyName = "鱼",
            FoodGroups = "动物性食品",
            FoodNutrientValues = []
        };
        var tower = new DietaryRecallTower(
        [
            new DietaryRecallEntry
            {
                Food = food,
                Weight = 100m,
                MealOccasion = MealOccasion.Lunch
            }
        ],
        standard);

        var projection = tower.ToSoapContribution();

        Assert.Contains("动物性食品", projection.Objective);
        Assert.Contains("蛋类", projection.Objective);
        Assert.Contains("水产品", projection.Objective);
        Assert.Contains("本次回顾折算量 100g", projection.Objective);
        Assert.DoesNotContain("实际食用量", projection.Objective);
        AssertNoAssessmentOrPlan(projection);
    }

    private static void AssertNoAssessmentOrPlan(SoapContribution contribution)
    {
        Assert.Null(contribution.Assessment);
        Assert.Null(contribution.Plan);
    }

    private static Nutrient Nutrient(int id, string name, string unit) => new()
    {
        NutrientId = id,
        FriendlyName = name,
        DefaultMeasureUnit = unit
    };

    private static Food Food(string name, Nutrient nutrient, decimal value)
    {
        var food = new Food
        {
            FoodId = Guid.NewGuid(),
            FriendlyCode = "TEST-FOOD",
            FriendlyName = name
        };
        food.FoodNutrientValues =
        [
            new FoodNutrientValue
            {
                Food = food,
                FoodId = food.FoodId,
                Nutrient = nutrient,
                NutrientId = nutrient.NutrientId,
                MeasureUnit = nutrient.DefaultMeasureUnit,
                Value = value
            }
        ];
        return food;
    }
}
