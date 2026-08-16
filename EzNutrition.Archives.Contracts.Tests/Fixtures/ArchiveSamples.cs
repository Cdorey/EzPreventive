using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Bundles;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Metadata;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Archives.Contracts.Tests.Fixtures;

/// <summary>
/// 提供不含真实身份、生产数据或随机值的确定性档案样本。
/// </summary>
internal static class ArchiveSamples
{
    private static readonly Uri SampleRoot = new("https://example.invalid/eznutrition-test/");
    private static readonly Uri UcumSystem = new("http://unitsofmeasure.org");
    private static readonly TimeSpan ChinaStandardTimeOffset = TimeSpan.FromHours(8);

    private static readonly ApplicationIdentity SampleApplication = new(
        new Uri(SampleRoot, "applications/archive-contract-tests"),
        "EzNutrition 档案契约测试程序",
        "1.0.0-test");

    private static readonly ActorReference SampleClinicalOrganization = new()
    {
        Kind = Code("actor-kind", "organization", "机构"),
        Identifier = new BusinessIdentifier(
            new Uri(SampleRoot, "identifiers/test-organizations"),
            "SYNTHETIC-ORGANIZATION-001"),
        Display = "虚构社区卫生服务中心"
    };

    private static readonly ActorReference SampleClinician = new()
    {
        Kind = Code("actor-kind", "practitioner", "医师"),
        Identifier = new BusinessIdentifier(
            new Uri(SampleRoot, "identifiers/test-clinicians"),
            "SYNTHETIC-CLINICIAN-001"),
        Display = "虚构测试医师",
        Organization = SampleClinicalOrganization
    };

    private static readonly ActorReference SampleTeachingOrganization = new()
    {
        Kind = Code("actor-kind", "organization", "机构"),
        Identifier = new BusinessIdentifier(
            new Uri(SampleRoot, "identifiers/test-organizations"),
            "SYNTHETIC-ORGANIZATION-002"),
        Display = "虚构营养学院"
    };

    private static readonly ActorReference SampleTeacher = new()
    {
        Kind = Code("actor-kind", "teacher", "教师"),
        Identifier = new BusinessIdentifier(
            new Uri(SampleRoot, "identifiers/test-users"),
            "SYNTHETIC-TEACHER-001"),
        Display = "虚构指导教师",
        Organization = SampleTeachingOrganization
    };

    private static readonly ActorReference SampleStudent = new()
    {
        Kind = Code("actor-kind", "student", "学生"),
        Identifier = new BusinessIdentifier(
            new Uri(SampleRoot, "identifiers/test-users"),
            "SYNTHETIC-STUDENT-001"),
        Display = "虚构学生",
        Organization = SampleTeachingOrganization
    };

    /// <summary>
    /// 获取十种合成档案情境。
    /// </summary>
    public static IReadOnlyList<ArchiveSample> All { get; } = Array.AsReadOnly(
        new[]
        {
            CreateMinimalAnonymousSample(),
            CreateComprehensiveAdultSample(),
            CreatePseudonymousPartialDateSample(),
            CreateMultiMealRecallSample(),
            CreateHistoricalSnapshotSample(),
            CreateSpecialPhysiologySample(),
            CreateAmendmentChainSample(),
            CreateExtensionsAndIdentifiersSample(),
            CreateTeachingReportSample(),
            CreateSyntheticScaleAssessmentSample()
        });

    /// <summary>
    /// 按稳定键获取一个必然存在的样本。
    /// </summary>
    /// <param name="key">样本稳定键。</param>
    /// <returns>对应的档案样本。</returns>
    public static ArchiveSample GetRequired(string key) =>
        All.Single(sample => string.Equals(sample.Key, key, StringComparison.Ordinal));

    private static ArchiveSample CreateMinimalAnonymousSample()
    {
        var patient = new PatientResource
        {
            Metadata = Metadata(101, 101, 1, ResourceLifecycleStatus.Draft),
            IdentityMode = PatientIdentityMode.Unlinked
        };

        var consultation = new ConsultationResource
        {
            Metadata = Metadata(102, 102, 1, ResourceLifecycleStatus.Draft),
            SubjectReference = PatientReference(101),
            Period = new Period(At(1, 9)),
            Title = "虚构最小匿名咨询"
        };

        return Sample(
            "minimal-anonymous",
            "仅包含未关联身份的咨询对象和可编辑咨询草稿。",
            1,
            ArchiveBundleType.ConsultationDocument,
            1,
            patient,
            consultation);
    }

    private static ArchiveSample CreateComprehensiveAdultSample()
    {
        var patient = new PatientResource
        {
            Metadata = Metadata(201, 201, 2),
            IdentityMode = PatientIdentityMode.Identified,
            Names = new[]
            {
                new HumanName
                {
                    Text = "虚构样本甲",
                    Family = "虚构",
                    Given = new[] { "样本甲" },
                    Use = Code("name-use", "official", "正式姓名")
                }
            },
            BusinessIdentifiers = new[]
            {
                new BusinessIdentifier(
                    new Uri(SampleRoot, "identifiers/synthetic-community-record"),
                    "SYNTHETIC-PATIENT-0201",
                    Code("identifier-type", "community-record", "社区档案号"),
                    "虚构社区卫生服务中心")
            },
            BirthDate = new PartialDate(1988, 5, 12),
            AdministrativeSex = Code("administrative-sex", "female", "女"),
            ManagingOrganization = new ActorReference
            {
                Kind = Code("actor-kind", "organization", "机构"),
                Display = "虚构社区卫生服务中心"
            }
        };

        var consultationReference = ExactReference(202, 202, ArchiveResourceTypes.Consultation);
        var consultation = new ConsultationResource
        {
            Metadata = Metadata(202, 202, 2),
            SubjectReference = PatientReference(201),
            Period = new Period(At(2, 9), At(2, 10)),
            SubjectSnapshot = new SubjectSnapshot
            {
                AgeAtConsultation = Quantity(37, "a", "年"),
                AdministrativeSex = Code("administrative-sex", "female", "女"),
                Height = Measurement(165, "cm", "厘米", ClinicalValueSourceKind.Measured, 2),
                Weight = Measurement(60, "kg", "千克", ClinicalValueSourceKind.Measured, 2),
                WaistCircumference = Measurement(72, "cm", "厘米", ClinicalValueSourceKind.Measured, 2),
                HipCircumference = Measurement(92, "cm", "厘米", ClinicalValueSourceKind.Measured, 2),
                IdentityDisplay = "虚构样本甲"
            },
            ClinicalResourceReferences = new[]
            {
                ExactReference(203, 203, ArchiveResourceTypes.EnergyAssessment),
                ExactReference(204, 204, ArchiveResourceTypes.DriAssessment),
                ExactReference(205, 205, ArchiveResourceTypes.DietaryRecall),
                ExactReference(206, 206, ArchiveResourceTypes.SoapNote),
                ExactReference(207, 207, ArchiveResourceTypes.NutritionAdvice)
            },
            Title = "虚构成人综合营养咨询",
            Reasons = new[] { Code("consultation-reason", "routine-assessment", "常规营养评估") },
            ServiceProvider = SampleClinician,
            LocationDisplay = "虚构全科诊室"
        };

        var energyAssessment = new EnergyAssessmentResource
        {
            Metadata = Metadata(203, 203, 2),
            SubjectReference = PatientReference(201),
            ConsultationReference = consultationReference,
            EffectiveAt = At(2, 9),
            CandidateCalculations = new[]
            {
                new EnergyCalculationCandidate
                {
                    CandidateId = new LocalIdentifier("candidate-1"),
                    Algorithm = Algorithm("synthetic-energy-formula", "1.0", "虚构能量公式"),
                    Inputs = new[]
                    {
                        AssessmentQuantity("weight", 60, "kg", "千克", ClinicalValueSourceKind.Measured),
                        AssessmentQuantity("height", 165, "cm", "厘米", ClinicalValueSourceKind.Measured),
                        AssessmentQuantity("age", 37, "a", "年", ClinicalValueSourceKind.Derived),
                        AssessmentDecimal("pal", 1.5m, ClinicalValueSourceKind.Reported)
                    },
                    ReferenceData = new[] { SyntheticDriReferenceData() },
                    Result = Quantity(1850, "kcal/d", "千卡/日"),
                    IntermediateResults = new[]
                    {
                        new NamedArchiveValue
                        {
                            Name = Code("energy-result", "basal-energy", "基础能量"),
                            Value = new QuantityArchiveValue(Quantity(1233, "kcal/d", "千卡/日"))
                        }
                    }
                }
            },
            ProfessionalDecision = new ProfessionalEnergyDecision
            {
                AdoptedEnergyTarget = Quantity(1800, "kcal/d", "千卡/日"),
                SelectedCandidateId = new LocalIdentifier("candidate-1"),
                DecisionBasis = Code("decision-basis", "adjusted-formula", "公式结果后专业修正"),
                Reason = "虚构示例：结合随访目标作小幅调整。"
            }
        };

        var driAssessment = new DriAssessmentResource
        {
            Metadata = Metadata(204, 204, 2),
            SubjectReference = PatientReference(201),
            ConsultationReference = consultationReference,
            EffectiveAt = At(2, 9),
            InputContext = new[]
            {
                AssessmentQuantity("age", 37, "a", "年", ClinicalValueSourceKind.Derived),
                AssessmentCoding("calculation-sex", "female", "女", ClinicalValueSourceKind.Reported)
            },
            Selector = Algorithm("synthetic-dri-selector", "1.0", "虚构 DRIs 人群选择器"),
            ReferenceData = SyntheticDriReferenceData(),
            PopulationGroup = new PopulationGroupSelection
            {
                BasisGroup = Code("dri-population", "female-18-49", "18～49 岁女性"),
                AdoptedGroup = Code("dri-population", "female-18-49", "18～49 岁女性")
            },
            NutrientResults = new[]
            {
                new NutrientReferenceResult
                {
                    Nutrient = Nutrient("protein", "蛋白质"),
                    ReferenceValues = new[]
                    {
                        new DriReferenceValue
                        {
                            ReferenceType = Code("dri-reference-type", "RNI", "推荐摄入量"),
                            BasisValue = new QuantityArchiveValue(Quantity(60, "g/d", "克/日")),
                            AdoptedValue = new QuantityArchiveValue(Quantity(60, "g/d", "克/日"))
                        }
                    }
                }
            }
        };

        var recall = CreateMultiMealRecall(205, 205, 201, 202, 202, 2);
        var soap = new SoapNoteResource
        {
            Metadata = Metadata(206, 206, 2),
            SubjectReference = PatientReference(201),
            ConsultationReference = consultationReference,
            EffectiveAt = At(2, 9),
            Subjective = "虚构主诉：希望了解日常膳食结构。",
            Objective = "虚构客观资料：测量数据见咨询快照。",
            Assessment = "虚构评估：当前资料可用于演示档案结构。",
            Plan = "虚构计划：按约定时间复核。"
        };
        var advice = new NutritionAdviceResource
        {
            Metadata = Metadata(207, 207, 2),
            SubjectReference = PatientReference(201),
            ConsultationReference = consultationReference,
            GenerationStatus = NutritionAdviceGenerationStatus.Completed,
            RequestedAt = At(2, 9),
            CompletedAt = At(2, 9),
            Generator = Algorithm("synthetic-advice-generator", "1.0", "虚构营养建议生成器"),
            InputResourceReferences = new[]
            {
                ExactReference(203, 203, ArchiveResourceTypes.EnergyAssessment),
                ExactReference(204, 204, ArchiveResourceTypes.DriAssessment),
                ExactReference(205, 205, ArchiveResourceTypes.DietaryRecall),
                ExactReference(206, 206, ArchiveResourceTypes.SoapNote)
            },
            InputSummary = new[]
            {
                new NamedArchiveValue
                {
                    Name = Code("advice-input", "adopted-energy", "核定能量"),
                    Value = new QuantityArchiveValue(Quantity(1800, "kcal/d", "千卡/日"))
                }
            },
            ReasoningContent = "虚构分析摘要。",
            NarrativeContent = "虚构营养建议正文。"
        };

        return Sample(
            "comprehensive-adult",
            "包含咨询、能量、DRIs、三餐膳食回忆、SOAP 和营养建议的完整成人档案。",
            2,
            ArchiveBundleType.ConsultationDocument,
            2,
            patient,
            consultation,
            energyAssessment,
            driAssessment,
            recall,
            soap,
            advice);
    }

    private static ArchiveSample CreatePseudonymousPartialDateSample()
    {
        var patient = new PatientResource
        {
            Metadata = Metadata(301, 301, 3),
            IdentityMode = PatientIdentityMode.Pseudonymous,
            Names = new[] { new HumanName { Text = "研究代号：虚构丙" } },
            BusinessIdentifiers = new[]
            {
                new BusinessIdentifier(
                    new Uri(SampleRoot, "identifiers/synthetic-research"),
                    "SYNTHETIC-STUDY-0301")
            },
            BirthDate = new PartialDate(1960),
            AdministrativeSex = Code("administrative-sex", "unknown", "未说明")
        };

        var consultation = new ConsultationResource
        {
            Metadata = Metadata(302, 302, 3),
            SubjectReference = PatientReference(301),
            Period = new Period(At(3, 14)),
            SubjectSnapshot = new SubjectSnapshot
            {
                AgeAtConsultation = Quantity(65, "a", "年"),
                IdentityDisplay = "研究代号：虚构丙"
            },
            Title = "仅具有部分人口学信息的虚构咨询"
        };

        return Sample(
            "pseudonymous-partial-date",
            "验证假名、仅年份出生日期和大量可选字段缺失。",
            3,
            ArchiveBundleType.ConsultationDocument,
            3,
            patient,
            consultation);
    }

    private static ArchiveSample CreateMultiMealRecallSample()
    {
        var patient = new PatientResource
        {
            Metadata = Metadata(401, 401, 4),
            IdentityMode = PatientIdentityMode.Unlinked
        };

        var consultation = new ConsultationResource
        {
            Metadata = Metadata(402, 402, 4),
            SubjectReference = PatientReference(401),
            Period = new Period(At(4, 9), At(4, 10)),
            ClinicalResourceReferences = new[]
            {
                ExactReference(403, 403, ArchiveResourceTypes.DietaryRecall)
            },
            Title = "虚构多餐次膳食回忆"
        };

        var recall = CreateMultiMealRecall(403, 403, 401, 402, 402, 4);

        return Sample(
            "multi-meal-recall",
            "三餐各含多个食物条目，餐次汇总、全日汇总与宏量能量可以复核。",
            4,
            ArchiveBundleType.ConsultationDocument,
            4,
            patient,
            consultation,
            recall);
    }

    private static ArchiveSample CreateHistoricalSnapshotSample()
    {
        var patient = new PatientResource
        {
            Metadata = Metadata(501, 501, 5),
            IdentityMode = PatientIdentityMode.Identified,
            Names = new[] { new HumanName { Text = "虚构当前称呼" } },
            AdministrativeSex = Code("administrative-sex", "female", "女")
        };

        var consultation = new ConsultationResource
        {
            Metadata = Metadata(502, 502, 5),
            SubjectReference = PatientReference(501),
            Period = new Period(At(5, 9), At(5, 10)),
            SubjectSnapshot = new SubjectSnapshot
            {
                AgeAtConsultation = Quantity(49, "a", "年"),
                AdministrativeSex = Code("administrative-sex", "female", "女"),
                Height = Measurement(158, "cm", "厘米", ClinicalValueSourceKind.Measured, 5),
                Weight = Measurement(54, "kg", "千克", ClinicalValueSourceKind.Measured, 5),
                IdentityDisplay = "虚构既往称呼"
            },
            Title = "验证历史快照不随当前资料变化"
        };

        return Sample(
            "historical-snapshot",
            "患者当前显示资料与咨询当时快照不同，历史快照仍保持原值。",
            5,
            ArchiveBundleType.ConsultationDocument,
            5,
            patient,
            consultation);
    }

    private static ArchiveSample CreateSpecialPhysiologySample()
    {
        var patient = new PatientResource
        {
            Metadata = Metadata(601, 601, 6),
            IdentityMode = PatientIdentityMode.Unlinked,
            AdministrativeSex = Code("administrative-sex", "male", "男")
        };

        var consultation = new ConsultationResource
        {
            Metadata = Metadata(602, 602, 6),
            SubjectReference = PatientReference(601),
            Period = new Period(At(6, 9), At(6, 10)),
            SubjectSnapshot = new SubjectSnapshot
            {
                AgeAtConsultation = Quantity(30, "a", "年"),
                AdministrativeSex = Code("administrative-sex", "male", "男"),
                PhysiologicalStates = new[]
                {
                    Code("physiological-state", "pregnancy-third-trimester", "孕晚期")
                }
            },
            ClinicalResourceReferences = new[]
            {
                ExactReference(603, 603, ArchiveResourceTypes.DriAssessment)
            },
            Title = "由医师判断的特殊生理状态虚构情境"
        };

        var driAssessment = new DriAssessmentResource
        {
            Metadata = Metadata(603, 603, 6),
            SubjectReference = PatientReference(601),
            ConsultationReference = ExactReference(602, 602, ArchiveResourceTypes.Consultation),
            EffectiveAt = At(6, 9),
            InputContext = new[]
            {
                AssessmentCoding("calculation-sex", "male", "男", ClinicalValueSourceKind.Reported),
                AssessmentCoding(
                    "physiological-state",
                    "pregnancy-third-trimester",
                    "孕晚期",
                    ClinicalValueSourceKind.Reported)
            },
            Selector = Algorithm("synthetic-dri-selector", "1.0", "虚构 DRIs 人群选择器"),
            ReferenceData = SyntheticDriReferenceData(),
            PopulationGroup = new PopulationGroupSelection
            {
                BasisGroup = Code("dri-population", "adult-male", "成年男性"),
                AdoptedGroup = Code("dri-population", "pregnancy-third-trimester", "孕晚期"),
                AdjustmentReason = "虚构测试：保留异常临床组合，交由医师判断。"
            }
        };

        return Sample(
            "special-physiology",
            "证明契约能够保存需要专业判断的罕见或表面矛盾临床组合。",
            6,
            ArchiveBundleType.ConsultationDocument,
            6,
            patient,
            consultation,
            driAssessment);
    }

    private static ArchiveSample CreateAmendmentChainSample()
    {
        var patient = new PatientResource
        {
            Metadata = Metadata(701, 701, 7),
            IdentityMode = PatientIdentityMode.Unlinked
        };

        var original = new SoapNoteResource
        {
            Metadata = Metadata(703, 7031, 7, ResourceLifecycleStatus.Final, 1),
            SubjectReference = PatientReference(701),
            EffectiveAt = At(7, 9),
            Assessment = "虚构原始评估。",
            Plan = "虚构原始计划。"
        };

        var amended = new SoapNoteResource
        {
            Metadata = Metadata(
                703,
                7032,
                8,
                ResourceLifecycleStatus.Amended,
                2,
                supersedes: ExactReference(703, 7031, ArchiveResourceTypes.SoapNote)),
            SubjectReference = PatientReference(701),
            EffectiveAt = At(7, 9),
            Assessment = "虚构修订后评估。",
            Plan = "虚构修订后计划，并保留原版本。"
        };

        return Sample(
            "amendment-chain",
            "同一 SOAP 逻辑资源包含正式版本和显式替代它的修订版本。",
            7,
            ArchiveBundleType.Collection,
            8,
            patient,
            original,
            amended);
    }

    private static ArchiveSample CreateExtensionsAndIdentifiersSample()
    {
        var structuredExtension = new ArchiveExtension(new Uri(SampleRoot, "extensions/synthetic-context"))
        {
            Children = new[]
            {
                new ArchiveExtension(new Uri(SampleRoot, "extensions/synthetic-context/site"))
                {
                    Value = new TextArchiveValue("虚构外勤点")
                },
                new ArchiveExtension(new Uri(SampleRoot, "extensions/synthetic-context/sequence"))
                {
                    Value = new IntegerArchiveValue(8)
                }
            }
        };

        var patient = new PatientResource
        {
            Metadata = Metadata(
                801,
                801,
                8,
                extensions: new[] { structuredExtension }),
            IdentityMode = PatientIdentityMode.Identified,
            Names = new[] { new HumanName { Text = "虚构样本辛" } },
            BusinessIdentifiers = new[]
            {
                new BusinessIdentifier(
                    new Uri(SampleRoot, "identifiers/synthetic-hospital-record"),
                    "SYNTHETIC-HOSPITAL-0801",
                    Code("identifier-type", "hospital-record", "院内档案号"),
                    "虚构医院")
            },
            BirthDate = new PartialDate(1992, 11)
        };

        var bundle = Bundle(
            8,
            ArchiveBundleType.TransferPackage,
            8,
            new IArchiveResource[] { patient }) with
        {
            Extensions = new[]
            {
                new ArchiveExtension(new Uri(SampleRoot, "extensions/export-purpose"))
                {
                    Value = new CodingArchiveValue(Code("export-purpose", "unit-test", "单元测试"))
                }
            }
        };

        return new ArchiveSample(
            "extensions-and-identifiers",
            "覆盖业务标识、年月精度日期、嵌套扩展和非 XML 源格式描述。",
            new ArchiveDocument
            {
                Bundle = bundle,
                SourceFormat = new ArchiveFormatDescriptor(
                    new Uri(SampleRoot, "formats/synthetic-memory-model"),
                    "1.0-test",
                    "application/vnd.eznutrition.synthetic+json")
            });
    }

    private static ArchiveSample CreateTeachingReportSample()
    {
        var patient = new PatientResource
        {
            Metadata = Metadata(901, 901, 9),
            IdentityMode = PatientIdentityMode.Pseudonymous,
            Names = [new HumanName { Text = "虚构教学对象" }]
        };
        var consultationReference = ExactReference(902, 902, ArchiveResourceTypes.Consultation);
        var consultation = new ConsultationResource
        {
            Metadata = Metadata(902, 902, 9),
            SubjectReference = PatientReference(901),
            Period = new Period(At(9, 8), At(9, 9)),
            ClinicalResourceReferences =
            [
                ExactReference(903, 903, ArchiveResourceTypes.SoapNote),
                ExactReference(904, 904, ArchiveResourceTypes.NutritionReport)
            ],
            Title = "虚构教学营养咨询",
            ServiceProvider = SampleTeacher
        };
        var soap = new SoapNoteResource
        {
            Metadata = Metadata(903, 903, 9),
            SubjectReference = PatientReference(901),
            ConsultationReference = consultationReference,
            EffectiveAt = At(9, 8),
            Assessment = "虚构教学评估。",
            Plan = "虚构教学计划。"
        };
        var report = new NutritionReportResource
        {
            Metadata = Metadata(904, 904, 9) with { FinalizedBy = SampleTeacher },
            SubjectReference = PatientReference(901),
            ConsultationReference = consultationReference,
            Purpose = Code("report-purpose", "teaching", "教学"),
            Title = "虚构教学营养报告",
            InputResourceReferences =
            [
                ExactReference(903, 903, ArchiveResourceTypes.SoapNote)
            ],
            PresentationTemplate = new CanonicalReference(
                new Uri(SampleRoot, "report-templates/teaching-summary"),
                "1.0"),
            RenderedArtifact = new ReportArtifactIdentity(
                "application/pdf",
                new ContentFingerprint(
                    Code("fingerprint-algorithm", "sha-256", "SHA-256"),
                    "1bd33a54f0879d51b727b90b5f3058fce96738ab88c460011cb9606e513b1df4")),
            Participants =
            [
                new ReportParticipation
                {
                    Function = Code("report-participation", "author", "作者"),
                    Actor = SampleStudent,
                    ActedAt = At(9, 8)
                },
                new ReportParticipation
                {
                    Function = Code("report-participation", "reviewer", "复核者"),
                    Actor = SampleTeacher,
                    ActedAt = At(9, 9)
                }
            ]
        };

        return Sample(
            "teaching-report",
            "学生编制、教师复核并签发的虚构教学营养报告，仅保存产物指纹。",
            9,
            ArchiveBundleType.ConsultationDocument,
            9,
            patient,
            consultation,
            soap,
            report);
    }

    private static ArchiveSample CreateSyntheticScaleAssessmentSample()
    {
        var patient = new PatientResource
        {
            Metadata = Metadata(1001, 1001, 10),
            IdentityMode = PatientIdentityMode.Pseudonymous,
            Names = [new HumanName { Text = "虚构量表对象" }]
        };
        var consultationReference = ExactReference(1002, 1002, ArchiveResourceTypes.Consultation);
        var consultation = new ConsultationResource
        {
            Metadata = Metadata(1002, 1002, 10),
            SubjectReference = PatientReference(1001),
            Period = new Period(At(10, 8), At(10, 9)),
            ClinicalResourceReferences =
            [
                ExactReference(1003, 1003, ArchiveResourceTypes.SoapNote),
                ExactReference(1004, 1004, ArchiveResourceTypes.NutritionScaleAssessment)
            ],
            Title = "虚构通用量表评估咨询",
            ServiceProvider = SampleClinician
        };
        var soap = new SoapNoteResource
        {
            Metadata = Metadata(1003, 1003, 10),
            SubjectReference = PatientReference(1001),
            ConsultationReference = consultationReference,
            EffectiveAt = At(10, 8),
            Assessment = "用于验证量表输入引用的虚构评估记录。"
        };
        var scale = new NutritionScaleAssessmentResource
        {
            Metadata = Metadata(1004, 1004, 10),
            SubjectReference = PatientReference(1001),
            ConsultationReference = consultationReference,
            EffectiveAt = At(10, 9),
            Instrument = new AssessmentInstrumentIdentity
            {
                Code = new Coding(
                    new Uri(SampleRoot, "codes/assessment-instrument"),
                    "synthetic-nutrition-screening",
                    display: "虚构营养筛查量表"),
                Version = "1.0-test",
                Definition = new CanonicalReference(
                    new Uri(SampleRoot, "assessment-instruments/synthetic-nutrition-screening"),
                    "1.0-test"),
                DefinitionFingerprint = new ContentFingerprint(
                    Code("fingerprint-algorithm", "sha-256", "SHA-256"),
                    "3d0ddf920785da54eabe959e5ddec2671c0340094e0e8ea9e953ed57c2c68d0e")
            },
            InputResourceReferences =
            [
                ExactReference(1003, 1003, ArchiveResourceTypes.SoapNote)
            ],
            Responses =
            [
                new AssessmentItemResponse
                {
                    Item = Code("synthetic-scale-item", "item-a", "虚构条目 A"),
                    Answer = new CodingArchiveValue(
                        Code("synthetic-scale-answer", "option-one", "虚构选项一")),
                    ScoreContribution = 1m
                },
                new AssessmentItemResponse
                {
                    Item = Code("synthetic-scale-item", "item-b", "虚构条目 B"),
                    Answer = new BooleanArchiveValue(false),
                    ScoreContribution = 0m
                }
            ],
            DerivedResults =
            [
                new NamedArchiveValue
                {
                    Name = Code("assessment-result", "answered-item-count", "已回答条目数"),
                    Value = new IntegerArchiveValue(2)
                }
            ],
            ScoringMethod = Algorithm("synthetic-scale-scoring", "1.0-test", "虚构量表评分方法"),
            TotalScore = 1m,
            Interpretation = Code("synthetic-scale-interpretation", "category-one", "虚构分类一"),
            Performer = SampleClinician
        };

        return Sample(
            "synthetic-scale-assessment",
            "不对应任何真实量表的通用结构样本，用于验证量表横向扩展能力。",
            10,
            ArchiveBundleType.ConsultationDocument,
            10,
            patient,
            consultation,
            soap,
            scale);
    }

    private static DietaryRecallResource CreateMultiMealRecall(
        int resourceNumber,
        int versionNumber,
        int patientResourceNumber,
        int consultationResourceNumber,
        int consultationVersionNumber,
        int day)
    {
        var breakfast = Meal(
            "meal-breakfast",
            "breakfast",
            "早餐",
            1,
            At(day, 7),
            new[]
            {
                Food("breakfast-1", "synthetic-oats", "虚构燕麦餐", 1, 60, 228, 8, 4, 40),
                Food("breakfast-2", "synthetic-milk", "虚构奶制品", 2, 200, 122, 6, 5, 12)
            },
            Nutrients(350, 14, 9, 52));

        var lunch = Meal(
            "meal-lunch",
            "lunch",
            "午餐",
            2,
            At(day, 12),
            new[]
            {
                Food("lunch-1", "synthetic-rice", "虚构谷物餐", 1, 250, 300, 6, 1, 66),
                Food("lunch-2", "synthetic-tofu-vegetables", "虚构豆制品蔬菜餐", 2, 300, 350, 23.5m, 15, 32)
            },
            Nutrients(650, 29.5m, 16, 98));

        var dinner = Meal(
            "meal-dinner",
            "dinner",
            "晚餐",
            3,
            At(day, 18),
            new[]
            {
                Food("dinner-1", "synthetic-noodles", "虚构面食", 1, 260, 400, 15, 8, 65),
                Food("dinner-2", "synthetic-fish-vegetables", "虚构鱼类蔬菜餐", 2, 280, 400, 29, 17, 35)
            },
            Nutrients(800, 44, 25, 100));

        return new DietaryRecallResource
        {
            Metadata = Metadata(resourceNumber, versionNumber, day),
            SubjectReference = PatientReference(patientResourceNumber),
            ConsultationReference = ExactReference(
                consultationResourceNumber,
                consultationVersionNumber,
                ArchiveResourceTypes.Consultation),
            RecallPeriod = new Period(At(day - 1, 0), At(day, 0)),
            RecallMethod = Code("dietary-recall-method", "24-hour-recall", "24 小时膳食回顾法"),
            Status = DietaryRecallStatus.IntakeReported,
            Meals = new[] { breakfast, lunch, dinner },
            TotalNutrientSummary = Nutrients(1800, 87.5m, 50, 250),
            EnergyConsistency = new DietaryEnergyConsistency
            {
                Method = Algorithm("synthetic-macro-energy", "1.0", "虚构宏量营养素能量折算"),
                RecordedTotalEnergy = Quantity(1800, "kcal", "千卡"),
                MacronutrientDerivedEnergy = Quantity(1800, "kcal", "千卡"),
                AllowedDifference = Quantity(20, "kcal", "千卡")
            }
        };
    }

    private static MealRecall Meal(
        string id,
        string occasionCode,
        string occasionDisplay,
        int sequence,
        DateTimeOffset consumedAt,
        IReadOnlyList<FoodIntakeEntry> entries,
        IReadOnlyList<NutrientAmount> summary) => new()
        {
            MealId = new LocalIdentifier(id),
            Occasion = Code("meal-occasion", occasionCode, occasionDisplay),
            ConsumedAt = consumedAt,
            Sequence = sequence,
            Entries = entries,
            NutrientSummary = summary
        };

    private static FoodIntakeEntry Food(
        string id,
        string foodCode,
        string foodDisplay,
        int sequence,
        decimal consumedGrams,
        decimal energy,
        decimal protein,
        decimal fat,
        decimal carbohydrate) => new()
        {
            EntryId = new LocalIdentifier(id),
            Food = Code("synthetic-food", foodCode, foodDisplay),
            ReportedAmount = Quantity(consumedGrams, "g", "克"),
            EdibleFraction = 1m,
            AdoptedConsumedAmount = Quantity(consumedGrams, "g", "克"),
            FoodCompositionData = SyntheticFoodCompositionData(),
            NutrientContributions = Nutrients(energy, protein, fat, carbohydrate),
            Sequence = sequence
        };

    private static IReadOnlyList<NutrientAmount> Nutrients(
        decimal energy,
        decimal protein,
        decimal fat,
        decimal carbohydrate) => new NutrientAmount[]
        {
            NutrientAmount("energy", "能量", energy, "kcal", "千卡"),
            NutrientAmount("protein", "蛋白质", protein, "g", "克"),
            NutrientAmount("fat", "脂肪", fat, "g", "克"),
            NutrientAmount("carbohydrate", "碳水化合物", carbohydrate, "g", "克")
        };

    private static NutrientAmount NutrientAmount(
        string code,
        string display,
        decimal value,
        string unitCode,
        string unitDisplay) => new()
        {
            Nutrient = Nutrient(code, display),
            Amount = Quantity(value, unitCode, unitDisplay)
        };

    private static ArchiveSample Sample(
        string key,
        string description,
        int bundleNumber,
        ArchiveBundleType bundleType,
        int day,
        params IArchiveResource[] resources) =>
        new(
            key,
            description,
            new ArchiveDocument { Bundle = Bundle(bundleNumber, bundleType, day, resources) });

    private static ArchiveBundle Bundle(
        int bundleNumber,
        ArchiveBundleType bundleType,
        int day,
        IReadOnlyList<IArchiveResource> resources) => new()
        {
            BundleId = BundleId(bundleNumber),
            BundleType = bundleType,
            CreatedAt = At(day, 11),
            Producer = SampleApplication,
            Entries = resources
        };

    private static ResourceMetadata Metadata(
        int resourceNumber,
        int versionNumber,
        int day,
        ResourceLifecycleStatus status = ResourceLifecycleStatus.Final,
        int revision = 1,
        VersionedResourceReference? supersedes = null,
        IReadOnlyList<ArchiveExtension>? extensions = null) => new()
        {
            ResourceId = ResourceId(resourceNumber),
            VersionId = VersionId(versionNumber),
            RevisionNumber = new RevisionNumber(revision),
            Status = status,
            CreatedAt = At(day, 8),
            LastModifiedAt = At(day, 9),
            FinalizedAt = status == ResourceLifecycleStatus.Draft ? null : At(day, 9),
            FinalizedBy = status == ResourceLifecycleStatus.Draft ? null : SampleClinician,
            Supersedes = supersedes,
            SourceApplication = SampleApplication,
            Extensions = extensions ?? Array.Empty<ArchiveExtension>()
        };

    private static ClinicalMeasurement Measurement(
        decimal value,
        string unitCode,
        string unitDisplay,
        ClinicalValueSourceKind sourceKind,
        int day) => new()
        {
            Value = Quantity(value, unitCode, unitDisplay),
            EffectiveAt = At(day, 9),
            SourceKind = sourceKind
        };

    private static AssessmentInput AssessmentQuantity(
        string parameterCode,
        decimal value,
        string unitCode,
        string unitDisplay,
        ClinicalValueSourceKind sourceKind) => new()
        {
            Parameter = Code("assessment-parameter", parameterCode, parameterCode),
            AdoptedValue = new QuantityArchiveValue(Quantity(value, unitCode, unitDisplay)),
            SourceKind = sourceKind
        };

    private static AssessmentInput AssessmentDecimal(
        string parameterCode,
        decimal value,
        ClinicalValueSourceKind sourceKind) => new()
        {
            Parameter = Code("assessment-parameter", parameterCode, parameterCode),
            AdoptedValue = new DecimalArchiveValue(value),
            SourceKind = sourceKind
        };

    private static AssessmentInput AssessmentCoding(
        string parameterCode,
        string valueCode,
        string valueDisplay,
        ClinicalValueSourceKind sourceKind) => new()
        {
            Parameter = Code("assessment-parameter", parameterCode, parameterCode),
            AdoptedValue = new CodingArchiveValue(Code(parameterCode, valueCode, valueDisplay)),
            SourceKind = sourceKind
        };

    private static AlgorithmIdentity Algorithm(string code, string version, string display) => new()
    {
        Method = new Coding(new Uri(SampleRoot, "codes/algorithm"), code, version, display),
        Implementation = SampleApplication
    };

    private static ReferenceDataIdentity SyntheticDriReferenceData() => new(
        new Uri(SampleRoot, "reference-data"),
        "synthetic-dri")
    {
        Edition = "test-edition",
        Release = "1.0-test",
        Fingerprint = new ContentFingerprint(
            Code("fingerprint-algorithm", "sha-256", "SHA-256"),
            new string('a', 64)),
        DerivedFrom = new[]
        {
            new CanonicalReference(new Uri(SampleRoot, "references/synthetic-dri-source"), "1.0-test")
        }
    };

    private static ReferenceDataIdentity SyntheticFoodCompositionData() => new(
        new Uri(SampleRoot, "reference-data"),
        "synthetic-food-composition")
    {
        Edition = "test-edition",
        Release = "1.0-test",
        FingerprintAbsentReason = DataAbsentReasonCode.NotApplicable
    };

    private static Quantity Quantity(decimal value, string unitCode, string unitDisplay) =>
        new(value, new Coding(UcumSystem, unitCode, display: unitDisplay));

    private static Coding Nutrient(string code, string display) =>
        Code("nutrient", code, display);

    private static Coding Code(string systemPath, string code, string display) =>
        new(new Uri(SampleRoot, $"codes/{systemPath}"), code, display: display);

    private static LogicalResourceReference PatientReference(int resourceNumber) =>
        new(ResourceId(resourceNumber), ArchiveResourceTypes.Patient);

    private static VersionedResourceReference ExactReference(
        int resourceNumber,
        int versionNumber,
        ResourceTypeCode resourceType) =>
        new(ResourceId(resourceNumber), VersionId(versionNumber), resourceType);

    private static ResourceId ResourceId(int value) =>
        new(Guid.Parse($"10000000-0000-0000-0000-{value:D12}"));

    private static ResourceVersionId VersionId(int value) =>
        new(Guid.Parse($"20000000-0000-0000-0000-{value:D12}"));

    private static ArchiveBundleId BundleId(int value) =>
        new(Guid.Parse($"30000000-0000-0000-0000-{value:D12}"));

    private static DateTimeOffset At(int day, int hour) =>
        new(2025, 1, day, hour, 0, 0, ChinaStandardTimeOffset);
}
