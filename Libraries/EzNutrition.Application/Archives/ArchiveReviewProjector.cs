using System.Globalization;
using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Application.Archives;

internal static class ArchiveReviewProjector
{
    public static ArchiveReview Create(ArchiveDocument document)
    {
        var bundle = document.Bundle;
        var patient = bundle.Entries.OfType<PatientResource>().SingleOrDefault();
        var consultation = bundle.Entries.OfType<ConsultationResource>().SingleOrDefault();
        var subject = PatientDisplay(patient, consultation);
        var title = consultation?.Title ?? $"{subject}的营养档案";
        var sections = new List<ArchiveReviewSection>();

        if (patient is not null || consultation is not null)
        {
            sections.Add(CreateConsultationSection(patient, consultation));
        }

        foreach (var resource in bundle.Entries.Where(resource =>
                     resource is not PatientResource and not ConsultationResource))
        {
            sections.Add(CreateResourceSection(resource));
        }

        var format = document.SourceFormat;
        return new ArchiveReview
        {
            BundleId = bundle.BundleId.Value,
            Title = title,
            SubjectDisplay = subject,
            CreatedAt = bundle.CreatedAt,
            FormatDisplay = format is null ? "当前应用档案" : FormatDisplay(format),
            ContainsUnknownContent = document.ContainsUnknownContent,
            PatientContext = patient is null ? null : new ArchivePatientContext(patient, consultation?.SubjectSnapshot),
            Sections = sections
        };
    }

    public static ArchiveRecordSummary CreateSummary(StoredArchiveDocumentInfo info) => new()
    {
        DocumentId = info.DocumentId,
        PatientId = info.PatientId,
        Title = info.Title,
        SubjectDisplay = info.SubjectDisplay,
        ConsultationStartedAt = info.ConsultationStartedAt,
        LastSavedAt = info.LastSavedAt
    };

    public static string PatientDisplay(PatientResource? patient, ConsultationResource? consultation)
    {
        var display = patient?.Names.Select(name => name.Text).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
        display ??= consultation?.SubjectSnapshot?.IdentityDisplay;
        return string.IsNullOrWhiteSpace(display) ? "未命名咨询对象" : display.Trim();
    }

    private static ArchiveReviewSection CreateConsultationSection(
        PatientResource? patient,
        ConsultationResource? consultation)
    {
        var snapshot = consultation?.SubjectSnapshot;
        var fields = new List<ArchiveReviewField>
        {
            Field("身份模式", FormatIdentityMode(patient?.IdentityMode)),
            DateTimeField("咨询开始", consultation?.Period.Start),
            DateTimeField("咨询结束", consultation?.Period.End),
            Field("性别", snapshot?.AdministrativeSex?.Display ?? patient?.AdministrativeSex?.Display ?? "未提供"),
            Field("年龄", FormatAge(snapshot)),
            Field("身高", FormatMeasurement(snapshot?.Height)),
            Field("体重", FormatMeasurement(snapshot?.Weight)),
            Field("生理状态", JoinDisplays(snapshot?.PhysiologicalStates))
        };

        return new ArchiveReviewSection
        {
            Title = "咨询概况",
            Description = consultation?.Title,
            Fields = fields
        };
    }

    private static ArchiveReviewSection CreateResourceSection(IArchiveResource resource) => resource switch
    {
        EnergyAssessmentResource energy => new ArchiveReviewSection
        {
            Title = "能量评估",
            Fields =
            [
                DateTimeField("评估时间", energy.EffectiveAt),
                Field("候选计算", energy.CandidateCalculations.Count.ToString(CultureInfo.InvariantCulture)),
                Field("采用能量", FormatQuantity(energy.ProfessionalDecision?.AdoptedEnergyTarget)),
                Field("决定依据", energy.ProfessionalDecision?.DecisionBasis?.Display ?? "尚未形成"),
                Field("调整说明", energy.ProfessionalDecision?.Reason ?? "无")
            ]
        },
        DriAssessmentResource dri => new ArchiveReviewSection
        {
            Title = "膳食参考摄入量",
            Fields =
            [
                DateTimeField("评估时间", dri.EffectiveAt),
                Field("采用人群", dri.PopulationGroup?.AdoptedGroup.Display ?? "尚未选择"),
                Field("营养素项目", dri.NutrientResults.Count.ToString(CultureInfo.InvariantCulture)),
                Field("参考数据", dri.ReferenceData is null
                    ? "未提供"
                    : $"{dri.ReferenceData.Code} {dri.ReferenceData.Edition ?? dri.ReferenceData.Release}".Trim())
            ]
        },
        DietaryRecallResource recall => new ArchiveReviewSection
        {
            Title = "膳食调查",
            Fields =
            [
                DateTimeField("最近修改", recall.Metadata.LastModifiedAt),
                Field("记录状态", FormatRecallStatus(recall.Status)),
                DateTimeField("回忆开始", recall.RecallPeriod?.Start),
                DateTimeField("回忆结束", recall.RecallPeriod?.End),
                Field("餐次", recall.Meals.Count.ToString(CultureInfo.InvariantCulture)),
                Field("食物条目", recall.Meals.Sum(meal => meal.Entries.Count).ToString(CultureInfo.InvariantCulture)),
                Field("记录总能量", FormatQuantity(recall.EnergyConsistency?.RecordedTotalEnergy))
            ]
        },
        NutritionScaleAssessmentResource scale => CreateNutritionScaleAssessmentSection(scale),
        SoapNoteResource soap => new ArchiveReviewSection
        {
            Title = "SOAP 病史",
            Fields =
            [
                DateTimeField("记录时间", soap.EffectiveAt),
                Field("主观资料", soap.Subjective ?? "未记录"),
                Field("客观资料", soap.Objective ?? "未记录"),
                Field("评估", soap.Assessment ?? "未记录"),
                Field("计划", soap.Plan ?? "未记录")
            ]
        },
        NutritionAdviceResource advice => new ArchiveReviewSection
        {
            Title = "营养建议",
            Description = $"生成状态：{FormatAdviceStatus(advice.GenerationStatus)}",
            Fields =
            [
                DateTimeField("生成时间", advice.CompletedAt ?? advice.RequestedAt),
                Field("建议正文", advice.NarrativeContent ?? "未形成"),
                Field("推理摘要", advice.ReasoningContent ?? "未记录")
            ]
        },
        _ => new ArchiveReviewSection
        {
            Title = resource.ResourceType.Value,
            Description = "当前查看器尚未提供该资源的专用展示。",
            Fields = [DateTimeField("最近修改", resource.Metadata.LastModifiedAt)]
        }
    };

    private static ArchiveReviewSection CreateNutritionScaleAssessmentSection(
        NutritionScaleAssessmentResource scale)
    {
        var version = scale.Instrument.Version ?? scale.Instrument.Definition?.Version ?? "未提供";
        var recordFields = new List<ArchiveReviewField>
        {
            DateTimeField("评估时间", scale.EffectiveAt),
            Field("量表编码", scale.Instrument.Code.Code),
            Field("量表版本", version),
            Field("调查人", FormatActor(scale.Performer))
        };
        if (scale.Performer?.Organization is { } organization)
        {
            recordFields.Add(Field("调查机构", FormatActor(organization)));
        }

        if (scale.Instrument.Definition is { } definition)
        {
            recordFields.Add(Field("定义来源", FormatCanonicalReference(definition)));
        }

        if (scale.Instrument.DefinitionFingerprint is { } fingerprint)
        {
            recordFields.Add(Field(
                "定义指纹",
                $"{FormatCoding(fingerprint.Algorithm)} · {fingerprint.Value}"));
        }

        if (scale.ScoringMethod is { } scoringMethod)
        {
            recordFields.Add(Field("计分方法", FormatCoding(scoringMethod.Method)));
            if (scoringMethod.Implementation is { } implementation)
            {
                recordFields.Add(Field("计分实现", $"{implementation.Name} {implementation.Version}"));
            }
        }

        var details = new List<ArchiveReviewDetailGroup>
        {
            new()
            {
                Title = "记录信息",
                Fields = recordFields
            }
        };
        if (scale.Responses.Count > 0)
        {
            details.Add(new ArchiveReviewDetailGroup
            {
                Title = "逐题作答",
                Description = $"档案共保存 {scale.Responses.Count.ToString(CultureInfo.InvariantCulture)} 项回答。",
                Fields = scale.Responses.Select(response => Field(
                    FormatCoding(response.Item),
                    FormatAssessmentResponse(response))).ToArray()
            });
        }

        if (scale.DerivedResults.Count > 0)
        {
            details.Add(new ArchiveReviewDetailGroup
            {
                Title = "派生结果",
                Fields = scale.DerivedResults.Select(result => Field(
                    FormatCoding(result.Name),
                    FormatArchiveValue(result.Value))).ToArray()
            });
        }

        details.Add(new ArchiveReviewDetailGroup
        {
            Title = "评估结论",
            Fields =
            [
                Field("总分", FormatAssessmentScore(scale.TotalScore, scale.TotalScoreAbsentReason)),
                Field("结果解释", scale.Interpretation is null
                    ? "尚未形成"
                    : FormatCoding(scale.Interpretation))
            ]
        });

        return new ArchiveReviewSection
        {
            Title = scale.Instrument.Code.Display ?? "营养筛查与评估量表",
            Description = scale.Interpretation?.Display ?? "量表尚未形成完整解释",
            Fields =
            [
                DateTimeField("评估时间", scale.EffectiveAt),
                Field("量表版本", version),
                Field("已回答题目", scale.Responses.Count.ToString(CultureInfo.InvariantCulture)),
                Field("总分", scale.TotalScore?.ToString(CultureInfo.InvariantCulture) ?? "不适用或尚未形成")
            ],
            DetailGroups = details
        };
    }

    private static string FormatAssessmentResponse(AssessmentItemResponse response)
    {
        var answer = response.Answer is null
            ? response.AnswerAbsentReason is { } absentReason
                ? FormatAbsentReason(absentReason)
                : "未提供"
            : FormatArchiveValue(response.Answer);
        return response.ScoreContribution is { } score
            ? $"{answer}（本题 {FormatDecimal(score)} 分）"
            : answer;
    }

    private static string FormatArchiveValue(ArchiveValue value) => value switch
    {
        TextArchiveValue text => text.Value,
        BooleanArchiveValue boolean => boolean.Value ? "是" : "否",
        IntegerArchiveValue integer => integer.Value.ToString(CultureInfo.InvariantCulture),
        DecimalArchiveValue number => FormatDecimal(number.Value),
        DateTimeArchiveValue instant => instant.Value.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
        PartialDateArchiveValue date => date.Value.ToString(),
        CodingArchiveValue coding => FormatCoding(coding.Value),
        CodingCollectionArchiveValue codings => string.Join("、", codings.Values.Select(FormatCoding)),
        QuantityArchiveValue quantity => FormatArchiveQuantity(quantity.Value),
        QuantityRangeArchiveValue range => FormatArchiveQuantityRange(range.Value),
        LogicalReferenceArchiveValue reference => FormatLogicalReference(reference.Value),
        VersionedReferenceArchiveValue reference =>
            $"{FormatLogicalReference(new LogicalResourceReference(reference.Value.ResourceId, reference.Value.ExpectedResourceType))}"
            + $" · 版本 {reference.Value.VersionId.Value:D}",
        _ => "当前查看器无法显示该档案值"
    };

    private static string FormatArchiveQuantity(Quantity quantity)
    {
        var comparator = quantity.Comparator switch
        {
            QuantityComparator.LessThan => "<",
            QuantityComparator.LessThanOrEqual => "≤",
            QuantityComparator.GreaterThanOrEqual => "≥",
            QuantityComparator.GreaterThan => ">",
            _ => string.Empty
        };
        var unit = quantity.Unit.Display ?? FormatUnit(quantity.Unit.Code);
        return $"{comparator}{FormatDecimal(quantity.Value)} {unit}".TrimEnd();
    }

    private static string FormatArchiveQuantityRange(QuantityRange range)
    {
        if (range.Low is { } low && range.High is { } high)
        {
            return $"{FormatArchiveQuantity(low)}～{FormatArchiveQuantity(high)}";
        }

        return FormatArchiveQuantity(range.Low ?? range.High!);
    }

    private static string FormatLogicalReference(LogicalResourceReference reference)
    {
        var identifier = reference.ResourceId.Value.ToString("D", CultureInfo.InvariantCulture);
        return reference.ExpectedResourceType is { } resourceType
            ? $"{resourceType.Value} · {identifier}"
            : identifier;
    }

    private static string FormatAssessmentScore(decimal? score, DataAbsentReasonCode? absentReason) =>
        score is { } value
            ? $"{FormatDecimal(value)} 分"
            : absentReason is { } reason
                ? FormatAbsentReason(reason)
                : "未提供";

    private static string FormatDecimal(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string FormatCoding(Coding coding) => coding.Display ?? coding.Code;

    private static string FormatCanonicalReference(CanonicalReference reference) =>
        reference.Version is { } version
            ? $"{reference.Uri.AbsoluteUri} · 版本 {version}"
            : reference.Uri.AbsoluteUri;

    private static string FormatActor(ActorReference? actor)
    {
        if (actor is null)
        {
            return "未提供";
        }

        if (!string.IsNullOrWhiteSpace(actor.Display))
        {
            return actor.Display;
        }

        if (actor.Identifier is { } identifier)
        {
            return identifier.Value;
        }

        if (actor.ResourceReference is { } reference)
        {
            return FormatLogicalReference(reference);
        }

        return actor.AbsentReason is { } absentReason
            ? FormatAbsentReason(absentReason)
            : "未提供";
    }

    private static string FormatAbsentReason(DataAbsentReasonCode reason) => reason switch
    {
        DataAbsentReasonCode.NotAsked => "未询问",
        DataAbsentReasonCode.NotApplicable => "不适用",
        DataAbsentReasonCode.Withheld => "信息已隐去",
        DataAbsentReasonCode.NotEstablished => "尚未建立或无法取得",
        DataAbsentReasonCode.Unsupported => "当前程序或格式不支持",
        _ => "原因未知"
    };

    private static ArchiveReviewField Field(string label, string value) => new(label, value);

    private static ArchiveReviewField DateTimeField(string label, DateTimeOffset? value) =>
        value is { } instant ? new ArchiveReviewField(label, instant) : Field(label, "未提供");

    private static string FormatMeasurement(ClinicalMeasurement? measurement) =>
        measurement is null ? "未提供" : FormatQuantity(measurement.Value);

    private static string FormatAge(SubjectSnapshot? snapshot) =>
        snapshot?.ChronologicalAgeAtConsultation?.ToString()
        ?? FormatQuantity(snapshot?.AgeAtConsultation);

    private static string FormatQuantity(Quantity? quantity)
    {
        if (quantity is null)
        {
            return "未提供";
        }

        var unit = quantity.Unit.Display ?? FormatUnit(quantity.Unit.Code);
        return FormattableString.Invariant($"{quantity.Value} {unit}");
    }

    private static string JoinDisplays(IEnumerable<Coding>? codings)
    {
        var values = codings?.Select(coding => coding.Display ?? coding.Code).ToArray() ?? [];
        return values.Length == 0 ? "无" : string.Join("、", values);
    }

    private static string FormatDisplay(ArchiveFormatDescriptor format)
    {
        var formatName = format.DisplayName ?? format.MediaType ?? format.Identifier.AbsoluteUri;
        return $"{formatName} · {format.Version}";
    }

    private static string FormatIdentityMode(PatientIdentityMode? mode) => mode switch
    {
        PatientIdentityMode.Identified => "已关联身份",
        PatientIdentityMode.Pseudonymous => "假名身份",
        PatientIdentityMode.Unlinked => "未关联身份",
        _ => "未提供"
    };

    private static string FormatRecallStatus(DietaryRecallStatus? status) => status switch
    {
        DietaryRecallStatus.IntakeReported => "已记录摄入",
        DietaryRecallStatus.NoIntake => "明确未摄入",
        _ => "草稿"
    };

    private static string FormatAdviceStatus(NutritionAdviceGenerationStatus status) => status switch
    {
        NutritionAdviceGenerationStatus.Prepared => "已准备",
        NutritionAdviceGenerationStatus.Generating => "生成中",
        NutritionAdviceGenerationStatus.Completed => "已完成",
        NutritionAdviceGenerationStatus.Incomplete => "内容不完整",
        NutritionAdviceGenerationStatus.Failed => "失败",
        _ => status.ToString()
    };

    private static string FormatUnit(string code) => code switch
    {
        "a" => "岁",
        "mo" => "月",
        "kg" => "kg",
        "g" => "g",
        "cm" => "cm",
        "kcal/d" => "千卡/日",
        "g/d" => "克/日",
        _ => code
    };
}
