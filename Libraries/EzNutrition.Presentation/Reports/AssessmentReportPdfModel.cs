using System.Globalization;
using EzNutrition.Application.Reports;
using EzNutrition.Application.Consultations;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Presentation.Reports;

/// <summary>向本机 PDF 模板提供已格式化的文字；不包含评分或签发权限规则。</summary>
internal sealed record AssessmentReportPdfModel
{
    public required string Title { get; init; }
    public required string ReportNumber { get; init; }
    /// <summary>获取报告的修订序号，与量表定义版本及排版模板版本分别表达。</summary>
    public required int RevisionNumber { get; init; }
    public required string InstrumentVersion { get; init; }
    public required string PatientName { get; init; }
    public required string Sex { get; init; }
    public required string Age { get; init; }
    public required string Height { get; init; }
    public required string Weight { get; init; }
    public required string AssessedAt { get; init; }
    public required string Performer { get; init; }
    public required string TotalScore { get; init; }
    public required string Interpretation { get; init; }
    public required string[][] Responses { get; init; }
    public required string[][] Results { get; init; }
    public required bool IsEvaluation { get; init; }
    public required string Signer { get; init; }
    public required string Institution { get; init; }
    public required string ReportTime { get; init; }

    /// <summary>将独立速查快照投影为评估稿；不虚构患者姓名、签发人或正式报告编号。</summary>
    public static AssessmentReportPdfModel From(NutritionAssessmentSnapshot snapshot, DateTimeOffset generatedAt) => new()
    {
        Title = $"{snapshot.Instrument.Code.Display} · 评估稿",
        ReportNumber = "",
        RevisionNumber = 0,
        InstrumentVersion = snapshot.Instrument.Version ?? "未记录",
        PatientName = "未关联患者（独立速查）",
        Sex = "未提供",
        Age = $"{snapshot.Subject.AgeInYears} 岁",
        Height = snapshot.Subject.HeightInCentimeters is { } height ? $"{Number(height)} cm" : "未提供",
        Weight = snapshot.Subject.WeightInKilograms is { } weight ? $"{Number(weight)} kg" : "未提供",
        AssessedAt = Time(snapshot.EffectiveAt),
        Performer = "未记录",
        TotalScore = Score(snapshot.TotalScore, snapshot.TotalScoreAbsentReason),
        Interpretation = snapshot.Interpretation?.Display ?? "量表未完成，暂无完整结果",
        Responses = ResponseRows(snapshot.Responses),
        Results = ResultRows(snapshot.DerivedResults),
        IsEvaluation = true,
        Signer = "未经医师审核签发",
        Institution = "",
        ReportTime = Time(generatedAt)
    };

    public static AssessmentReportPdfModel From(AssessmentReportDraft draft)
    {
        var scale = draft.Assessment;
        var subject = draft.Document.Bundle.Entries.OfType<ConsultationResource>().Single().SubjectSnapshot;
        return new AssessmentReportPdfModel
        {
            Title = draft.Report.Title ?? "营养量表报告",
            ReportNumber = draft.Report.Metadata.ResourceId.Value.ToString("D"),
            RevisionNumber = draft.Report.Metadata.RevisionNumber.Value,
            InstrumentVersion = scale.Instrument.Version ?? "未记录",
            PatientName = subject?.IdentityDisplay ?? "未关联患者",
            Sex = subject?.AdministrativeSex?.Display ?? "未提供",
            Age = subject?.ChronologicalAgeAtConsultation?.ToString() ?? "未提供",
            Height = Measurement(subject?.Height),
            Weight = Measurement(subject?.Weight),
            AssessedAt = Time(scale.EffectiveAt),
            Performer = scale.Performer?.Display ?? "未记录",
            TotalScore = Score(scale.TotalScore, scale.TotalScoreAbsentReason),
            Interpretation = scale.Interpretation?.Display ?? "量表未完成，暂无完整结果",
            Responses = ResponseRows(scale.Responses),
            Results = ResultRows(scale.DerivedResults),
            IsEvaluation = draft.Signer is null,
            Signer = draft.Signer?.Display ?? "未经医师审核签发",
            Institution = draft.Signer?.Organization?.Display ?? "",
            ReportTime = Time(draft.Report.Metadata.CreatedAt)
        };
    }

    private static string Score(decimal? score, DataAbsentReasonCode? absentReason) => score is { } value
        ? Number(value) + " 分" : absentReason == DataAbsentReasonCode.NotApplicable ? "不适用" : "尚未完成";

    private static string[][] ResponseRows(IEnumerable<AssessmentItemResponse> responses) => responses.Select(response => new[]
    {
        response.Item.Display ?? response.Item.Code,
        response.Answer is null ? "未回答" : Answer(response.Answer),
        response.ScoreContribution is { } contribution ? Number(contribution) : "—"
    }).ToArray();

    private static string[][] ResultRows(IEnumerable<NamedArchiveValue> results) => results.Select(result => new[]
        { result.Name.Display ?? result.Name.Code, Answer(result.Value) }).ToArray();

    private static string Answer(ArchiveValue answer) => answer switch
    {
        CodingArchiveValue coding => coding.Value.Display ?? coding.Value.Code,
        CodingCollectionArchiveValue collection => string.Join("、", collection.Values.Select(value => value.Display ?? value.Code)),
        DecimalArchiveValue number => Number(number.Value),
        QuantityArchiveValue quantity => $"{Number(quantity.Value.Value)} {quantity.Value.Unit.Display ?? quantity.Value.Unit.Code}",
        IntegerArchiveValue integer => integer.Value.ToString(CultureInfo.InvariantCulture),
        TextArchiveValue text => text.Value,
        // 未支持的值不能静默丢失后继续签发。
        _ => throw new NotSupportedException("当前量表报告模板尚不支持该回答类型。")
    };

    private static string Measurement(ClinicalMeasurement? measurement) => measurement?.Value is { } value
        ? $"{Number(value.Value)} {value.Unit.Display ?? value.Unit.Code}" : "未提供";

    private static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    // 日期在生成时固定文字，历史打印不会随设备时区或区域设置变化。
    private static string Time(DateTimeOffset value) => value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
}
