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
        var reports = bundle.Entries.OfType<NutritionReportResource>().ToArray();
        var superseded = reports.Where(item => item.Metadata.Supersedes is not null)
            .Select(item => item.Metadata.Supersedes!.VersionId).ToHashSet();
        var heads = reports.Where(item => !superseded.Contains(item.Metadata.VersionId)).ToArray();
        var report = heads.Length == 1 ? heads[0] : null;
        var consultations = bundle.Entries.OfType<ConsultationResource>().ToArray();
        var consultation = report is null ? (consultations.Length == 1 ? consultations[0] : null)
            : consultations.SingleOrDefault(item => item.Metadata.VersionId == report.ConsultationReference.VersionId);
        var patients = bundle.Entries.OfType<PatientResource>().ToArray();
        var patient = report is null ? (patients.Length == 1 ? patients[0] : null)
            : patients.SingleOrDefault(item => report.InputResourceReferences.Any(reference => reference.VersionId == item.Metadata.VersionId));
        var subject = PatientDisplay(patient, consultation);
        var title = report?.Title ?? consultation?.Title ?? $"{subject}的营养档案";
        var sections = new List<ArchiveReviewSection>();

        if (patient is not null || consultation is not null)
        {
            sections.Add(CreateConsultationSection(patient, consultation));
        }

        foreach (var resource in bundle.Entries.Where(resource =>
                     resource is not PatientResource and not ConsultationResource
                     && (report is null || resource is NutritionReportResource
                         || report.InputResourceReferences.Any(reference => reference.VersionId == resource.Metadata.VersionId)))
                     .OrderBy(resource => resource is NutritionReportResource version
                         ? superseded.Contains(version.Metadata.VersionId) ? 2 : 0 : 1))
        {
            var section = CreateResourceSection(resource);
            if (resource is NutritionReportResource version)
                section = section with
                {
                    Title = $"报告第 {version.Metadata.RevisionNumber.Value} 版",
                    Description = superseded.Contains(version.Metadata.VersionId)
                        ? "已被后续签发版本替代；历史原件保留在报告包中。" : section.Description,
                    Fields = superseded.Contains(version.Metadata.VersionId)
                        ? section.Fields.Select(field => field.Label == "状态"
                            ? new ArchiveReviewField("状态", "已被替代（原件保留）") : field).ToArray()
                        : section.Fields
                };
            sections.Add(section);
        }

        var format = document.SourceFormat;
        return new ArchiveReview
        {
            BundleId = bundle.BundleId.Value,
            Title = title,
            SubjectDisplay = subject,
            CreatedAt = bundle.CreatedAt,
            FormatDisplay = report is not null ? "报告档案" : format is null ? "当前应用档案" : FormatDisplay(format),
            ContainsUnknownContent = document.ContainsUnknownContent,
            PatientContext = patient is null ? null : new ArchivePatientContext(patient, consultation?.SubjectSnapshot),
            Sections = sections
        };
    }

    public static ArchiveRecordSummary CreateSummary(StoredArchiveDocumentInfo info) => new()
    {
        IsReport = info.FormatIdentifier == Reports.ReportPackage.Format.Identifier.AbsoluteUri,
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
        NutritionReportResource report => new ArchiveReviewSection
        {
            Title = report.Title ?? "营养报告",
            Description = "本页展示报告来源与签发信息；再次打印应读取保存的 PDF 原件。",
            Fields =
            [
                Field("报告编号", report.Metadata.ResourceId.Value.ToString("D")),
                Field("版本", report.Metadata.RevisionNumber.Value.ToString(CultureInfo.InvariantCulture)),
                Field("状态", report.Metadata.Status switch
                {
                    EzNutrition.Archives.Contracts.Metadata.ResourceLifecycleStatus.Final => "已签发",
                    EzNutrition.Archives.Contracts.Metadata.ResourceLifecycleStatus.Amended => "已更正签发",
                    EzNutrition.Archives.Contracts.Metadata.ResourceLifecycleStatus.EnteredInError => "已标记错误",
                    _ => "草稿"
                }),
                Field("用途", FormatCoding(report.Purpose)),
                Field("签发人", FormatActor(report.Metadata.FinalizedBy)),
                DateTimeField("签发时间", report.Metadata.FinalizedAt)
            ]
        },
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
