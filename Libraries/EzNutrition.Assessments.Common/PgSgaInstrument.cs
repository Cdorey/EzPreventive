using System.Globalization;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;

namespace EzNutrition.Assessments.Common;

/// <summary>
/// 按 WS/T 555—2017 实现肿瘤患者主观整体营养评估（PG-SGA）。
/// </summary>
/// <remarks>
/// 评估对象、记录项目、评分规则、结果分级与干预层级依据 WS/T 555—2017
/// 第 3～4 章、规范性附录 A 表 A.1 及附录 B。
/// </remarks>
public sealed class PgSgaInstrument : INutritionAssessmentInstrument
{
    private const string CurrentWeightCode = "current-weight";
    private const string WeightReferenceCode = "weight-reference";
    private const string ReferenceWeightCode = "reference-weight";
    private const string SubjectiveWeightLossCode = "subjective-weight-loss";
    private const string TwoWeekWeightCode = "two-week-weight-trend";
    private const string IntakeChangeCode = "one-month-intake-change";
    private const string CurrentIntakeCode = "current-intake";
    private const string SymptomsCode = "nutrition-impact-symptoms";
    private const string ActivityCode = "activity-and-function";
    private const string ComorbidityCode = "comorbidities";
    private const string FeverCode = "fever";
    private const string FeverDurationCode = "fever-duration";
    private const string SteroidCode = "fever-related-steroid-dose";
    private const string MuscleLossCode = "overall-muscle-loss";

    private static readonly string[] AlwaysRequiredCodes =
    [
        WeightReferenceCode,
        TwoWeekWeightCode,
        IntakeChangeCode,
        CurrentIntakeCode,
        SymptomsCode,
        ActivityCode,
        ComorbidityCode,
        FeverCode,
        MuscleLossCode
    ];

    private static readonly NutritionAssessmentDefinition InstrumentDefinition = new()
    {
        CodeSystem = new Uri("https://eznutrition.cdorey.net/codes/nutrition-assessment"),
        Code = "pg-sga",
        Version = "WS/T 555—2017",
        DefinitionUri = new Uri(
            "https://www.nhc.gov.cn/wjw/yingyang/201708/fb23588e8ea64da7ae93c8d81b1fa663/files/1739783542007_50021.pdf"),
        DisplayName = "肿瘤患者主观整体营养评估 PG-SGA",
        Description =
            "本量表依据 WS/T 555—2017《肿瘤患者主观整体营养评估》第 3～4 章、规范性附录 A 表 A.1 及附录 B。适用于年龄 18 岁以上、病理确诊为恶性肿瘤、神志清楚、无交流障碍、愿意接受评估且非濒临死亡的患者。",
        Sections =
        [
            new NutritionAssessmentSection(
                "patient-weight",
                "1 体重（患者自评）",
                [
                    DecimalItem(
                        CurrentWeightCode,
                        "目前体重",
                        "已有咨询体重时直接采用咨询开始时的快照；缺失时在此补录。"),
                    new NutritionAssessmentItem(
                        WeightReferenceCode,
                        "用于计算体重下降率的资料",
                        [
                            new NutritionAssessmentOption("one-month", "有 1 个月前体重"),
                            new NutritionAssessmentOption("six-month", "无 1 个月资料，采用 6 个月前体重"),
                            new NutritionAssessmentOption("subjective", "无法准确了解具体体重，按下降程度自评")
                        ],
                        "标准规定优先采用 1 个月体重变化；没有 1 个月资料时采用 6 个月资料。"),
                    DecimalItem(
                        ReferenceWeightCode,
                        "所选时点的既往体重",
                        "按上一题选择填写 1 个月前或 6 个月前体重。"),
                    new NutritionAssessmentItem(
                        SubjectiveWeightLossCode,
                        "无法准确了解具体体重时，自评体重下降程度",
                        [
                            Scored("none", "无", 0),
                            Scored("mild", "轻", 1),
                            Scored("moderate", "中", 2),
                            Scored("severe", "重", 3),
                            Scored("very-severe", "极重", 4)
                        ]),
                    new NutritionAssessmentItem(
                        TwoWeekWeightCode,
                        "最近 2 周体重变化",
                        [
                            Scored("unchanged-or-increased", "无改变或增加", 0),
                            Scored("decreased", "下降", 1)
                        ])
                ]),
            new NutritionAssessmentSection(
                "patient-intake-symptoms-function",
                "2～4 进食、症状、活动和身体功能（患者自评）",
                [
                    new NutritionAssessmentItem(
                        IntakeChangeCode,
                        "过去 1 个月进食情况与平时相比",
                        [
                            Scored("unchanged", "无变化", 0),
                            Scored("greater", "大于平常", 0),
                            Scored("less", "小于平常", 1)
                        ]),
                    new NutritionAssessmentItem(
                        CurrentIntakeCode,
                        "目前进食方式与食量",
                        [
                            Scored("normal", "正常饮食", 0),
                            Scored("normal-but-less", "正常饮食，但比正常情况少", 1),
                            Scored("small-solid", "进食少量固体食物", 2),
                            Scored("liquid-only", "只能进食流质食物", 3),
                            Scored("oral-supplements-only", "只能口服营养制剂", 3),
                            Scored("almost-none", "几乎吃不下食物", 4),
                            Scored("tube-or-parenteral", "只能依赖管饲或静脉营养", 0)
                        ],
                        "第 2 项按所选内容中的最高分计分。"),
                    new NutritionAssessmentItem(
                        SymptomsCode,
                        "近 2 周经常出现并影响饮食的问题（可多选）",
                        [
                            Scored("none", "没有饮食问题", 0, true),
                            Scored("nausea", "恶心", 1),
                            Scored("dry-mouth", "口干", 1),
                            Scored("constipation", "便秘", 1),
                            Scored("no-taste", "食物没有味道", 1),
                            Scored("bad-smell", "食物气味不好", 1),
                            Scored("early-satiety", "吃一会儿就饱了", 1),
                            Scored("other", "其他（如抑郁、经济问题、牙齿问题）", 1),
                            Scored("mouth-sores", "口腔溃疡", 2),
                            Scored("difficulty-swallowing", "吞咽困难", 2),
                            Scored("diarrhea", "腹泻", 3),
                            Scored("vomiting", "呕吐", 3),
                            Scored("pain", "疼痛", 3),
                            Scored("no-appetite", "没有食欲，不想吃饭", 3)
                        ],
                        "本项累计计分；偶尔一次出现的症状不作为选择。",
                        NutritionAssessmentResponseType.MultipleChoice),
                    new NutritionAssessmentItem(
                        ActivityCode,
                        "过去 1 个月活动和身体功能",
                        [
                            Scored("normal", "正常，无限制", 0),
                            Scored("slightly-worse", "与平常相比稍差，但尚能正常活动", 1),
                            Scored("reluctant-up-under-12-hours", "多数时候不想起床活动，但卧床或坐着不超过 12 h", 2),
                            Scored("mostly-bed-or-chair", "活动很少，一天多数时间卧床或坐着", 3),
                            Scored("almost-bedridden", "几乎卧床不起，很少下床", 3)
                        ])
                ]),
            new NutritionAssessmentSection(
                "professional-assessment",
                "5～7 医务人员评估",
                [
                    new NutritionAssessmentItem(
                        ComorbidityCode,
                        "合并疾病（可多选，累计计分）",
                        [
                            Scored("none", "无表列合并疾病", 0, true),
                            Scored("cancer", "肿瘤", 1),
                            Scored("aids", "艾滋病", 1),
                            Scored("cardiac-or-respiratory-cachexia", "呼吸或心脏疾病恶液质", 1),
                            Scored("wound-fistula-or-pressure-injury", "开放性伤口、肠瘘或压疮", 1),
                            Scored("trauma", "创伤", 1)
                        ],
                        "标准表列疾病可单选或多选并累计计分；年龄 >65 岁另加 1 分。",
                        NutritionAssessmentResponseType.MultipleChoice),
                    new NutritionAssessmentItem(
                        FeverCode,
                        "本次评估时的发热程度",
                        [
                            Scored("none", "无发热", 0),
                            Scored("mild", "37.2 ℃～38.3 ℃", 1),
                            Scored("moderate", "38.3 ℃～38.8 ℃", 2),
                            Scored("severe", ">38.8 ℃", 3)
                        ]),
                    new NutritionAssessmentItem(
                        FeverDurationCode,
                        "本次发热已经持续的时间",
                        [
                            Scored("below-72-hours", "<72 h", 1),
                            Scored("72-hours", "72 h", 2),
                            Scored("above-72-hours", ">72 h", 3)
                        ]),
                    new NutritionAssessmentItem(
                        SteroidCode,
                        "因本次发热使用的糖皮质激素（按强的松或相当剂量）",
                        [
                            Scored("none", "未使用", 0),
                            Scored("below-10", "<10 mg/d", 1),
                            Scored("10-to-30", "10 mg/d～30 mg/d", 2),
                            Scored("above-30", ">30 mg/d", 3)
                        ]),
                    new NutritionAssessmentItem(
                        MuscleLossCode,
                        "总体肌肉丢失评分",
                        [
                            Scored("none", "0 分：无肌肉丢失", 0),
                            Scored("mild", "1 分：轻度肌肉丢失", 1),
                            Scored("moderate", "2 分：中度肌肉丢失", 2),
                            Scored("severe", "3 分：重度肌肉丢失", 3)
                        ],
                        "依次检查颞肌、锁骨部位、肩部、肩胛部、手背骨间肌、大腿和小腿，按多数部位的情况确定总体评分。")
                ])
        ]
    };

    /// <inheritdoc />
    public NutritionAssessmentDefinition Definition => InstrumentDefinition;

    /// <inheritdoc />
    public NutritionAssessmentEvaluation Evaluate(
        IReadOnlyDictionary<string, NutritionAssessmentAnswer> answers,
        NutritionAssessmentSubject subject)
    {
        ArgumentNullException.ThrowIfNull(answers);
        ArgumentNullException.ThrowIfNull(subject);
        NutritionAssessmentInstrumentAnswers.Validate(InstrumentDefinition, answers);

        var applicable = new HashSet<string>(AlwaysRequiredCodes, StringComparer.Ordinal);
        var required = new List<string>(AlwaysRequiredCodes);
        if (subject.WeightInKilograms is null)
        {
            applicable.Add(CurrentWeightCode);
            required.Add(CurrentWeightCode);
        }

        if (answers.TryGetValue(WeightReferenceCode, out _))
        {
            var selectedWeightReference = NutritionAssessmentInstrumentAnswers.Single(
                answers,
                WeightReferenceCode);
            var conditionalWeightCode = selectedWeightReference == "subjective"
                ? SubjectiveWeightLossCode
                : ReferenceWeightCode;
            applicable.Add(conditionalWeightCode);
            required.Add(conditionalWeightCode);
        }

        if (answers.TryGetValue(FeverCode, out _)
            && NutritionAssessmentInstrumentAnswers.Single(answers, FeverCode) != "none")
        {
            applicable.Add(FeverDurationCode);
            applicable.Add(SteroidCode);
            required.Add(FeverDurationCode);
            required.Add(SteroidCode);
        }

        var missing = NutritionAssessmentInstrumentAnswers.Missing(answers, required);
        if (missing.Count > 0)
        {
            return new NutritionAssessmentEvaluation
            {
                IsComplete = false,
                ApplicableItemCodes = applicable,
                MissingItemCodes = missing
            };
        }

        var currentWeight = subject.WeightInKilograms
            ?? NutritionAssessmentInstrumentAnswers.Decimal(answers, CurrentWeightCode);
        var weightReference = NutritionAssessmentInstrumentAnswers.Single(
            answers,
            WeightReferenceCode);
        var (weightLossScore, weightLossPercentage) = WeightLossScore(
            answers,
            currentWeight,
            weightReference);
        var twoWeekScore = Score(answers, TwoWeekWeightCode);
        var weightScore = weightLossScore + twoWeekScore;
        var intakeScore = Math.Max(
            Score(answers, IntakeChangeCode),
            Score(answers, CurrentIntakeCode));
        var symptomScore = NutritionAssessmentInstrumentAnswers.SumSelectedScores(
            InstrumentDefinition,
            answers,
            SymptomsCode);
        var activityScore = Score(answers, ActivityCode);
        var patientScore = weightScore + intakeScore + symptomScore + activityScore;

        var comorbidityScore = NutritionAssessmentInstrumentAnswers.SumSelectedScores(
            InstrumentDefinition,
            answers,
            ComorbidityCode);
        var ageScore = subject.AgeInYears > 65 ? 1m : 0m;
        var professionalDiseaseScore = comorbidityScore + ageScore;
        var stressScore = Score(answers, FeverCode);
        if (NutritionAssessmentInstrumentAnswers.Single(answers, FeverCode) != "none")
        {
            stressScore += Score(answers, FeverDurationCode) + Score(answers, SteroidCode);
        }

        var physicalScore = Score(answers, MuscleLossCode);
        var total = patientScore + professionalDiseaseScore + stressScore + physicalScore;
        var interpretation = Interpretation(total);
        var totalText = total.ToString(CultureInfo.InvariantCulture);
        var metrics = new List<NutritionAssessmentMetric>
        {
            new("patient-score", "患者自评 A 评分", patientScore),
            new("disease-score", "合并疾病 B 评分", professionalDiseaseScore),
            new("stress-score", "应激 C 评分", stressScore),
            new("physical-examination-score", "体格检查 D 评分", physicalScore),
            new("weight-score", "体重评分", weightScore),
            new("age-score", "年龄评分", ageScore)
        };
        if (weightLossPercentage is { } percentage)
        {
            metrics.Add(new NutritionAssessmentMetric(
                "weight-loss-percentage",
                weightReference == "one-month" ? "1 个月体重下降率" : "6 个月体重下降率",
                percentage));
        }

        return new NutritionAssessmentEvaluation
        {
            IsComplete = true,
            ApplicableItemCodes = applicable,
            MissingItemCodes = [],
            TotalScore = total,
            Metrics = metrics,
            Interpretation = interpretation,
            SoapContribution = new SoapContribution
            {
                Objective =
                    $"肿瘤患者主观整体营养评估（PG-SGA，WS/T 555—2017）：A {patientScore:0} 分，B {professionalDiseaseScore:0} 分，C {stressScore:0} 分，D {physicalScore:0} 分，总分 {totalText} 分。",
                Assessment = $"PG-SGA 结果：{interpretation.Display}（{totalText} 分）。",
                Plan = Plan(total)
            }
        };
    }

    private static (decimal Score, decimal? Percentage) WeightLossScore(
        IReadOnlyDictionary<string, NutritionAssessmentAnswer> answers,
        decimal currentWeight,
        string reference)
    {
        if (reference == "subjective")
        {
            return (Score(answers, SubjectiveWeightLossCode), null);
        }

        var referenceWeight = NutritionAssessmentInstrumentAnswers.Decimal(
            answers,
            ReferenceWeightCode);
        var percentage = referenceWeight > 0m
            ? Math.Max(0m, (referenceWeight - currentWeight) / referenceWeight * 100m)
            : 0m;
        return reference switch
        {
            "one-month" => (percentage switch
            {
                >= 10m => 4m,
                >= 5m => 3m,
                >= 3m => 2m,
                >= 2m => 1m,
                _ => 0m
            }, percentage),
            "six-month" => (percentage switch
            {
                >= 20m => 4m,
                >= 10m => 3m,
                >= 6m => 2m,
                >= 2m => 1m,
                _ => 0m
            }, percentage),
            _ => throw new InvalidOperationException("PG-SGA 体重资料时点无效。")
        };
    }

    private static NutritionAssessmentInterpretation Interpretation(decimal total) => total switch
    {
        <= 1m => new NutritionAssessmentInterpretation(
            "well-nourished",
            "营养良好",
            NutritionAssessmentAttentionLevel.Routine),
        <= 3m => new NutritionAssessmentInterpretation(
            "suspected-or-mild-malnutrition",
            "可疑或轻度营养不良",
            NutritionAssessmentAttentionLevel.RequiresAttention),
        <= 8m => new NutritionAssessmentInterpretation(
            "moderate-malnutrition",
            "中度营养不良",
            NutritionAssessmentAttentionLevel.RequiresAttention),
        _ => new NutritionAssessmentInterpretation(
            "severe-malnutrition",
            "重度营养不良",
            NutritionAssessmentAttentionLevel.RequiresAttention)
    };

    private static string Plan(decimal total) => total switch
    {
        <= 1m => "治疗期间保持常规随诊及评估。",
        <= 3m => "由营养师、护师或医生进行患者或家庭教育，并结合症状及检查结果考虑相应干预。",
        <= 8m => "由营养师进行干预，并可根据症状严重程度开展多专业联合营养干预。",
        _ => "急需改善症状和/或同时进行营养干预。"
    };

    private static decimal Score(
        IReadOnlyDictionary<string, NutritionAssessmentAnswer> answers,
        string itemCode) => NutritionAssessmentInstrumentAnswers.Score(
            InstrumentDefinition,
            answers,
            itemCode);

    private static NutritionAssessmentOption Scored(
        string code,
        string display,
        int score,
        bool isExclusive = false) => new(code, display, score, isExclusive);

    private static NutritionAssessmentItem DecimalItem(
        string code,
        string prompt,
        string helpText) => new(
            code,
            prompt,
            [],
            helpText,
            NutritionAssessmentResponseType.Decimal,
            "kg",
            1m,
            500m);
}
