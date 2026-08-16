using System.Text;
using System.Xml.Linq;
using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Bundles;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Metadata;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.Validation;
using EzNutrition.Archives.Contracts.ValueObjects;

namespace EzNutrition.Archives.Xml.Tests;

public sealed class XmlArchiveCodecTests
{
    [Fact]
    public void Xml_implementation_depends_on_contracts_but_not_application_or_ui_layers()
    {
        var references = typeof(XmlArchiveCodec).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.Contains("EzNutrition.Archives.Contracts", references);
        Assert.DoesNotContain("EzNutrition.Application", references);
        Assert.DoesNotContain("EzNutrition.UI", references);
        Assert.DoesNotContain("EzNutrition.Client", references);
        Assert.DoesNotContain("Microsoft.AspNetCore.Components", references);
    }

    [Fact]
    public async Task Current_consultation_document_round_trips_as_versioned_xml()
    {
        var codec = CreateCodec();
        var source = CreateDocument();
        await using var stream = new MemoryStream();

        var write = await codec.WriteAsync(new ArchiveWriteRequest
        {
            Document = source,
            TargetFormat = XmlArchiveFormat.Current
        }, stream);
        var xml = Encoding.UTF8.GetString(stream.ToArray());
        stream.Position = 0;
        var read = await codec.ReadAsync(stream);

        Assert.True(write.IsSuccess);
        Assert.True(read.IsSuccess);
        Assert.Contains("resourceType=\"Patient\"", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("EzNutrition.Archives.Contracts", xml, StringComparison.Ordinal);
        Assert.Equal(source.Bundle.BundleId, read.Document?.Bundle.BundleId);
        Assert.Equal(source.Bundle.Entries.Count, read.Document?.Bundle.Entries.Count);
        var patient = Assert.Single(read.Document!.Bundle.Entries.OfType<PatientResource>());
        Assert.Equal("虚构测试对象", Assert.Single(patient.Names).Text);
    }

    /// <summary>
    /// 验证报告用途、参与职责、行为时机构快照、签发者和产物身份均可往返。
    /// </summary>
    [Fact]
    public async Task Nutrition_report_provenance_round_trips_as_current_xml()
    {
        var codec = CreateCodec();
        var source = CreateReportDocument();
        var bytes = await WriteAsync(codec, source);

        await using var stream = new MemoryStream(bytes);
        var read = await codec.ReadAsync(stream);

        Assert.True(read.IsSuccess);
        Assert.Contains("resourceType=\"NutritionReport\"", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        var report = Assert.Single(read.Document!.Bundle.Entries.OfType<NutritionReportResource>());
        Assert.Equal("teaching", report.Purpose.Code);
        Assert.Equal("虚构营养学院", report.Metadata.FinalizedBy?.Organization?.Display);
        Assert.Equal("teacher", report.Metadata.FinalizedBy?.Kind?.Code);
        Assert.Equal("student", Assert.Single(report.Participants).Actor.Kind?.Code);
        Assert.Equal("application/pdf", report.RenderedArtifact?.MediaType);
        Assert.Equal(new string('b', 64), report.RenderedArtifact?.Fingerprint.Value);
    }

    [Fact]
    public async Task Unknown_xml_is_preserved_until_known_semantics_change()
    {
        var codec = CreateCodec();
        var sourceBytes = await WriteAsync(codec, CreateDocument());
        var xml = XDocument.Parse(Encoding.UTF8.GetString(sourceBytes), LoadOptions.PreserveWhitespace);
        XNamespace ns = XmlArchiveFormat.Namespace;
        var patientContent = xml.Descendants(ns + "Entry")
            .Single(entry => (string?)entry.Attribute("resourceType") == "Patient")
            .Element(ns + "Content")!;
        patientContent.Add(new XElement(ns + "FutureField", new XAttribute("kind", "string"), "保留内容"));
        var extendedBytes = Encoding.UTF8.GetBytes(xml.ToString(SaveOptions.DisableFormatting));

        await using var input = new MemoryStream(extendedBytes);
        var read = await codec.ReadAsync(input);
        var unchangedBytes = await WriteAsync(codec, read.Document!);

        Assert.True(read.IsSuccess);
        Assert.True(read.ContainsUnknownContent);
        Assert.Contains("FutureField", Encoding.UTF8.GetString(unchangedBytes), StringComparison.Ordinal);

        var patient = Assert.Single(read.Document!.Bundle.Entries.OfType<PatientResource>());
        var changedPatient = patient with { Names = [new HumanName { Text = "修改后的姓名" }] };
        var changedDocument = read.Document with
        {
            Bundle = read.Document.Bundle with
            {
                Entries = read.Document.Bundle.Entries
                    .Select(resource => ReferenceEquals(resource, patient) ? changedPatient : resource)
                    .ToArray()
            }
        };
        await using var rejectedOutput = new MemoryStream();
        var rejected = await codec.WriteAsync(new ArchiveWriteRequest
        {
            Document = changedDocument,
            TargetFormat = XmlArchiveFormat.Current
        }, rejectedOutput);

        Assert.False(rejected.IsSuccess);
        Assert.Contains(rejected.Validation.Issues, issue =>
            issue.Code == XmlArchiveValidationCodes.UnknownContentConflict);
        Assert.Equal(0, rejectedOutput.Length);
    }

    [Fact]
    public async Task Dtd_and_external_entities_are_rejected_without_disclosing_input()
    {
        const string malicious = "<!DOCTYPE x [<!ENTITY probe SYSTEM 'file:///secret'>]>" +
                                 "<ArchiveDocument xmlns='https://eznutrition.cdorey.net/formats/archive-xml/1' " +
                                 "formatVersion='1.0'>&probe;</ArchiveDocument>";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(malicious));

        var result = await CreateCodec().ReadAsync(stream);

        Assert.False(result.IsSuccess);
        var issue = Assert.Single(result.Validation.Issues);
        Assert.Equal(XmlArchiveValidationCodes.InvalidDocument, issue.Code);
        Assert.DoesNotContain("secret", issue.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unsupported_format_version_returns_a_compatibility_issue()
    {
        var codec = CreateCodec();
        var bytes = await WriteAsync(codec, CreateDocument());
        var xml = Encoding.UTF8.GetString(bytes).Replace(
            "formatVersion=\"1.0\"",
            "formatVersion=\"99.0\"",
            StringComparison.Ordinal);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        var result = await codec.ReadAsync(stream);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Validation.Issues, issue =>
            issue.Code == XmlArchiveValidationCodes.UnsupportedVersion &&
            issue.Category == ArchiveValidationCategory.Compatibility);
    }

    private static XmlArchiveCodec CreateCodec() => new(new ArchiveContractValidator());

    private static async Task<byte[]> WriteAsync(XmlArchiveCodec codec, ArchiveDocument document)
    {
        await using var stream = new MemoryStream();
        var result = await codec.WriteAsync(new ArchiveWriteRequest
        {
            Document = document,
            TargetFormat = XmlArchiveFormat.Current
        }, stream);
        Assert.True(result.IsSuccess, string.Join(" | ", result.Validation.Issues.Select(issue => issue.Code)));
        return stream.ToArray();
    }

    private static ArchiveDocument CreateDocument()
    {
        var time = new DateTimeOffset(2026, 8, 14, 9, 0, 0, TimeSpan.FromHours(8));
        var application = new ApplicationIdentity(
            new Uri("https://example.invalid/applications/xml-tests"),
            "XML 档案测试",
            "1.0");
        var patientId = new ResourceId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var consultationId = new ResourceId(Guid.Parse("10000000-0000-0000-0000-000000000002"));
        var patient = new PatientResource
        {
            Metadata = Metadata(patientId, 1, time, application),
            IdentityMode = PatientIdentityMode.Identified,
            Names = [new HumanName { Text = "虚构测试对象" }],
            AdministrativeSex = new Coding(
                new Uri("https://example.invalid/codes/sex"),
                "female",
                display: "女")
        };
        var consultation = new ConsultationResource
        {
            Metadata = Metadata(consultationId, 2, time, application),
            SubjectReference = new LogicalResourceReference(patientId, ArchiveResourceTypes.Patient),
            Period = new Period(time),
            SubjectSnapshot = new SubjectSnapshot
            {
                IdentityDisplay = "虚构测试对象",
                AgeAtConsultation = new Quantity(
                    30,
                    new Coding(new Uri("http://unitsofmeasure.org"), "a", display: "年"))
            },
            Title = "虚构营养咨询"
        };

        return new ArchiveDocument
        {
            Bundle = new ArchiveBundle
            {
                BundleId = new ArchiveBundleId(Guid.Parse("30000000-0000-0000-0000-000000000001")),
                BundleType = ArchiveBundleType.ConsultationDocument,
                CreatedAt = time,
                Producer = application,
                Entries = new IArchiveResource[] { patient, consultation }
            }
        };
    }

    private static ArchiveDocument CreateReportDocument()
    {
        var source = CreateDocument();
        var patient = source.Bundle.Entries.OfType<PatientResource>().Single();
        var consultation = source.Bundle.Entries.OfType<ConsultationResource>().Single();
        var time = consultation.Metadata.CreatedAt;
        var organization = new ActorReference
        {
            Kind = new Coding(new Uri("https://example.invalid/codes/actor-kind"), "organization", display: "机构"),
            Identifier = new BusinessIdentifier(
                new Uri("https://example.invalid/identifiers/organizations"),
                "SYNTHETIC-UNIVERSITY-001"),
            Display = "虚构营养学院"
        };
        var teacher = new ActorReference
        {
            Kind = new Coding(new Uri("https://example.invalid/codes/actor-kind"), "teacher", display: "教师"),
            Identifier = new BusinessIdentifier(
                new Uri("https://example.invalid/identifiers/users"),
                "SYNTHETIC-TEACHER-001"),
            Display = "虚构指导教师",
            Organization = organization
        };
        var student = new ActorReference
        {
            Kind = new Coding(new Uri("https://example.invalid/codes/actor-kind"), "student", display: "学生"),
            Identifier = new BusinessIdentifier(
                new Uri("https://example.invalid/identifiers/users"),
                "SYNTHETIC-STUDENT-001"),
            Display = "虚构学生",
            Organization = organization
        };
        var reportId = new ResourceId(Guid.Parse("10000000-0000-0000-0000-000000000003"));
        var report = new NutritionReportResource
        {
            Metadata = Metadata(reportId, 3, time, consultation.Metadata.SourceApplication) with
            {
                Status = ResourceLifecycleStatus.Final,
                FinalizedAt = time,
                FinalizedBy = teacher
            },
            SubjectReference = new LogicalResourceReference(patient.Metadata.ResourceId, ArchiveResourceTypes.Patient),
            ConsultationReference = new VersionedResourceReference(
                consultation.Metadata.ResourceId,
                consultation.Metadata.VersionId,
                ArchiveResourceTypes.Consultation),
            Purpose = new Coding(
                new Uri("https://example.invalid/codes/report-purpose"),
                "teaching",
                display: "教学"),
            Title = "虚构教学营养报告",
            PresentationTemplate = new CanonicalReference(
                new Uri("https://example.invalid/report-templates/teaching-summary"),
                "1.0"),
            RenderedArtifact = new ReportArtifactIdentity(
                "application/pdf",
                new ContentFingerprint(
                    new Coding(
                        new Uri("https://example.invalid/codes/fingerprint-algorithm"),
                        "sha-256",
                        display: "SHA-256"),
                    new string('b', 64))),
            Participants =
            [
                new ReportParticipation
                {
                    Function = new Coding(
                        new Uri("https://example.invalid/codes/report-participation"),
                        "author",
                        display: "作者"),
                    Actor = student,
                    ActedAt = time
                }
            ]
        };
        var reportReference = new VersionedResourceReference(
            report.Metadata.ResourceId,
            report.Metadata.VersionId,
            ArchiveResourceTypes.NutritionReport);
        var changedConsultation = consultation with { ClinicalResourceReferences = [reportReference] };

        return source with
        {
            Bundle = source.Bundle with
            {
                Entries = new IArchiveResource[] { patient, changedConsultation, report }
            }
        };
    }

    private static ResourceMetadata Metadata(
        ResourceId resourceId,
        int number,
        DateTimeOffset time,
        ApplicationIdentity application) => new()
        {
            ResourceId = resourceId,
            VersionId = new ResourceVersionId(Guid.Parse($"20000000-0000-0000-0000-{number:D12}")),
            RevisionNumber = new RevisionNumber(1),
            Status = ResourceLifecycleStatus.Draft,
            CreatedAt = time,
            LastModifiedAt = time,
            SourceApplication = application
        };
}
