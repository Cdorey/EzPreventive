using EzNutrition.Application.Archives;
using EzNutrition.Archives.Contracts.Abstractions;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.Validation;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Archives.Xml;
using EzNutrition.Client.Tests.Fixtures;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Domain.Consultations;

namespace EzNutrition.Client.Tests.Tests;

/// <summary>
/// 验证当前运行态咨询的完整资源图可以通过 XML 往返。
/// </summary>
public sealed class XmlArchiveRuntimeRoundTripTests
{
    private static readonly ArchiveContractAssembler Assembler = new(new ApplicationIdentity(
        new Uri("https://example.invalid/applications/runtime-xml-tests"),
        "运行态 XML 测试",
        "1.0"));
    private static readonly ArchiveContractValidator Validator = new();

    [Fact]
    public async Task All_runtime_archive_samples_round_trip_through_xml()
    {
        var samples = await RuntimeArchiveSamples.CreateAllAsync();
        var codec = new XmlArchiveCodec(Validator);

        foreach (var sample in samples)
        {
            var source = Assembler.CreateDocument(
                sample.Archive,
                sample.Archive.ContractIdentity.CreatedAt.AddHours(1));
            await using var stream = new MemoryStream();
            var write = await codec.WriteAsync(new ArchiveWriteRequest
            {
                Document = source,
                TargetFormat = XmlArchiveFormat.Current
            }, stream);
            stream.Position = 0;
            var read = await codec.ReadAsync(stream);

            Assert.True(
                write.IsSuccess,
                $"{sample.Key} write: {string.Join(" | ", write.Validation.Issues.Select(issue => issue.Code))}");
            Assert.True(
                read.IsSuccess,
                $"{sample.Key} read: {string.Join(" | ", read.Validation.Issues.Select(issue => issue.Code))}");
            Assert.False(read.ContainsUnknownContent);
            Assert.Equal(ResourceKeys(source.Bundle.Entries), ResourceKeys(read.Document!.Bundle.Entries));
            Assert.False(Validator.ValidateBundle(
                read.Document.Bundle,
                ArchiveValidationScope.Import).HasErrors);
        }
    }

    [Fact]
    public async Task Birth_date_and_structured_age_survive_runtime_xml_round_trip()
    {
        var client = new ClientInfo
        {
            Name = "合成儿保对象",
            Gender = "女",
            BirthDate = new DateOnly(2024, 4, 17),
            Age = new EzNutrition.Domain.Consultations.ChronologicalAge(1, 4, 23)
        };
        var workspace = new EzNutrition.Application.Consultations.ConsultationWorkspace(client);
        var source = Assembler.CreateDocument(workspace, workspace.ContractIdentity.CreatedAt.AddMinutes(1));
        var codec = new XmlArchiveCodec(Validator);
        await using var stream = new MemoryStream();

        var write = await codec.WriteAsync(new ArchiveWriteRequest
        {
            Document = source,
            TargetFormat = XmlArchiveFormat.Current
        }, stream);
        stream.Position = 0;
        var read = await codec.ReadAsync(stream);

        Assert.True(write.IsSuccess);
        Assert.True(read.IsSuccess);
        var patient = Assert.Single(read.Document!.Bundle.Entries.OfType<PatientResource>());
        var consultation = Assert.Single(read.Document.Bundle.Entries.OfType<ConsultationResource>());
        Assert.Equal(new PartialDate(2024, 4, 17), patient.BirthDate);
        var age = Assert.IsType<EzNutrition.Archives.Contracts.ValueObjects.ChronologicalAge>(
            consultation.SubjectSnapshot?.ChronologicalAgeAtConsultation);
        Assert.Equal(1, age.Years);
        Assert.Equal(4, age.Months);
        Assert.Equal(23, age.Days);
        Assert.Equal(1m, consultation.SubjectSnapshot?.AgeAtConsultation?.Value);
    }

    private static IReadOnlyList<string> ResourceKeys(IEnumerable<IArchiveResource> resources) => resources
        .Select(resource => $"{resource.ResourceType.Value}/{resource.Metadata.ResourceId}/{resource.Metadata.VersionId}")
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();
}
