using EzNutrition.Domain.Assessments;

namespace EzNutrition.Assessments.Common;

internal static class NutritionAssessmentInstrumentAnswers
{
    public static void Validate(
        NutritionAssessmentDefinition definition,
        IReadOnlyDictionary<string, NutritionAssessmentAnswer> answers)
    {
        foreach (var (itemCode, answer) in answers)
        {
            var item = definition.Items.SingleOrDefault(candidate => string.Equals(
                candidate.Code,
                itemCode,
                StringComparison.Ordinal))
                ?? throw InvalidAnswer(itemCode);
            switch (item.ResponseType, answer)
            {
                case (NutritionAssessmentResponseType.SingleChoice,
                    NutritionAssessmentSingleChoiceAnswer singleChoice):
                    _ = Option(definition, itemCode, singleChoice.OptionCode);
                    break;
                case (NutritionAssessmentResponseType.MultipleChoice,
                    NutritionAssessmentMultipleChoiceAnswer multipleChoice):
                    var selected = multipleChoice.OptionCodes
                        .Select(optionCode => Option(definition, itemCode, optionCode))
                        .ToArray();
                    if (selected.Length > 1 && selected.Any(option => option.IsExclusive))
                    {
                        throw InvalidAnswer(itemCode);
                    }

                    break;
                case (NutritionAssessmentResponseType.Decimal,
                    NutritionAssessmentDecimalAnswer number)
                    when (item.MinimumValue is null || number.Value >= item.MinimumValue)
                        && (item.MaximumValue is null || number.Value <= item.MaximumValue):
                    break;
                default:
                    throw InvalidAnswer(itemCode);
            }
        }
    }

    public static string Single(
        IReadOnlyDictionary<string, NutritionAssessmentAnswer> answers,
        string itemCode) => answers.TryGetValue(itemCode, out var answer)
            && answer is NutritionAssessmentSingleChoiceAnswer singleChoice
                ? singleChoice.OptionCode
                : throw InvalidAnswer(itemCode);

    public static IReadOnlyList<string> Multiple(
        IReadOnlyDictionary<string, NutritionAssessmentAnswer> answers,
        string itemCode) => answers.TryGetValue(itemCode, out var answer)
            && answer is NutritionAssessmentMultipleChoiceAnswer multipleChoice
                ? multipleChoice.OptionCodes
                : throw InvalidAnswer(itemCode);

    public static decimal Decimal(
        IReadOnlyDictionary<string, NutritionAssessmentAnswer> answers,
        string itemCode) => answers.TryGetValue(itemCode, out var answer)
            && answer is NutritionAssessmentDecimalAnswer number
                ? number.Value
                : throw InvalidAnswer(itemCode);

    public static decimal Score(
        NutritionAssessmentDefinition definition,
        IReadOnlyDictionary<string, NutritionAssessmentAnswer> answers,
        string itemCode) => Option(
            definition,
            itemCode,
            Single(answers, itemCode)).Score
            ?? throw new InvalidOperationException($"量表题目 {itemCode} 的选项缺少分值。");

    public static decimal SumSelectedScores(
        NutritionAssessmentDefinition definition,
        IReadOnlyDictionary<string, NutritionAssessmentAnswer> answers,
        string itemCode) => Multiple(answers, itemCode)
            .Sum(optionCode => Option(definition, itemCode, optionCode).Score
                ?? throw new InvalidOperationException(
                    $"量表题目 {itemCode} 的选项缺少分值。"));

    public static NutritionAssessmentOption Option(
        NutritionAssessmentDefinition definition,
        string itemCode,
        string optionCode) => definition.Items
            .Single(item => string.Equals(item.Code, itemCode, StringComparison.Ordinal))
            .Options
            .SingleOrDefault(option => string.Equals(
                option.Code,
                optionCode,
                StringComparison.Ordinal))
            ?? throw InvalidAnswer(itemCode);

    public static IReadOnlyList<string> Missing(
        IReadOnlyDictionary<string, NutritionAssessmentAnswer> answers,
        IEnumerable<string> requiredItemCodes) => requiredItemCodes
            .Where(code => !answers.ContainsKey(code))
            .ToArray();

    private static ArgumentException InvalidAnswer(string itemCode) => new(
        $"量表回答包含未知题目、错误题型或无效选项：{itemCode}。",
        "answers");
}
