using EzNutrition.Application.Archives;
using EzNutrition.Application.Consultations;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;

namespace EzNutrition.Application.Tests.Consultations;

/// <summary>
/// 验证通用量表运行态对多选和数值题的约束及档案映射。
/// </summary>
public sealed class NutritionAssessmentTypedAnswerTests
{
    /// <summary>
    /// 验证类型化回答会保持稳定集合语义，并映射为对应的档案值。
    /// </summary>
    [Fact]
    public void Multiple_choice_and_decimal_answers_are_validated_and_archived()
    {
        var workspace = new ConsultationWorkspace(new ClientInfo
        {
            Gender = "女",
            Age = new EzNutrition.Domain.Consultations.ChronologicalAge(68),
            Height = 165m,
            Weight = 60m
        });
        var service = new NutritionAssessmentApplicationService([new TypedInstrument()]);
        var run = service.StartRun(
            workspace,
            Assert.Single(service.Definitions),
            workspace.ContractIdentity.CreatedAt);

        Assert.Throws<ArgumentException>(() =>
            run.SetMultipleChoiceAnswer("symptoms", ["none", "nausea"]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            run.SetDecimalAnswer("previous-weight", 501m));

        Assert.True(run.SetMultipleChoiceAnswer("symptoms", ["nausea", "dry-mouth"]));
        Assert.False(run.SetMultipleChoiceAnswer("symptoms", ["dry-mouth", "nausea"]));
        Assert.True(run.SetDecimalAnswer("previous-weight", 65.5m));
        Assert.True(run.Evaluation.IsComplete);

        var resource = Assert.Single(
            new ArchiveContractAssembler(new ApplicationIdentity(
                    new Uri("https://eznutrition.cdorey.net/applications/typed-answer-test"),
                    "类型化回答测试",
                    "2.1-test"))
                .CreateDocument(workspace, workspace.ContractIdentity.CreatedAt.AddMinutes(1))
                .Bundle.Entries
                .OfType<NutritionScaleAssessmentResource>());

        var multipleChoice = Assert.IsType<CodingCollectionArchiveValue>(
            resource.Responses.Single(response =>
                response.Item.Code.EndsWith("/symptoms", StringComparison.Ordinal)).Answer);
        Assert.Equal(
            ["nausea", "dry-mouth"],
            multipleChoice.Values.Select(value => value.Code.Split('/')[^1]));
        Assert.Equal(
            65.5m,
            Assert.IsType<DecimalArchiveValue>(resource.Responses.Single(response =>
                response.Item.Code.EndsWith("/previous-weight", StringComparison.Ordinal)).Answer).Value);
    }

    private sealed class TypedInstrument : INutritionAssessmentInstrument
    {
        private static readonly IReadOnlySet<string> ItemCodes =
            new HashSet<string>(["symptoms", "previous-weight"], StringComparer.Ordinal);

        public NutritionAssessmentDefinition Definition { get; } = new()
        {
            CodeSystem = new Uri("https://example.invalid/codes/assessment"),
            Code = "typed-test",
            Version = "1.0-test",
            DefinitionUri = new Uri("https://example.invalid/assessments/typed-test"),
            DisplayName = "类型化量表测试",
            Description = "用于验证多选和数值回答的合成量表。",
            Sections =
            [
                new NutritionAssessmentSection(
                    "typed",
                    "类型化回答",
                    [
                        new NutritionAssessmentItem(
                            "symptoms",
                            "症状",
                            [
                                new NutritionAssessmentOption("none", "无", 0m, true),
                                new NutritionAssessmentOption("nausea", "恶心", 1m),
                                new NutritionAssessmentOption("dry-mouth", "口干", 1m)
                            ],
                            ResponseType: NutritionAssessmentResponseType.MultipleChoice),
                        new NutritionAssessmentItem(
                            "previous-weight",
                            "既往体重",
                            [],
                            ResponseType: NutritionAssessmentResponseType.Decimal,
                            Unit: "kg",
                            MinimumValue: 1m,
                            MaximumValue: 500m)
                    ])
            ]
        };

        public NutritionAssessmentEvaluation Evaluate(
            IReadOnlyDictionary<string, NutritionAssessmentAnswer> answers,
            NutritionAssessmentSubject subject)
        {
            var missing = ItemCodes.Where(code => !answers.ContainsKey(code)).ToArray();
            return new NutritionAssessmentEvaluation
            {
                IsComplete = missing.Length == 0,
                ApplicableItemCodes = ItemCodes,
                MissingItemCodes = missing,
                TotalScore = missing.Length == 0 ? 0m : null,
                Interpretation = missing.Length == 0
                    ? new NutritionAssessmentInterpretation("complete", "已完成")
                    : null
            };
        }
    }
}
