using System.Text;
using EzNutrition.Application.Archives;
using EzNutrition.Application.Consultations;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.Validation;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Domain.Consultations;

namespace EzNutrition.Application.Tests.Archives;

public sealed class ArchiveWorkflowTests
{
    [Fact]
    public async Task Default_workflow_saves_browses_and_opens_without_exposing_the_codec_to_ui()
    {
        var fixture = CreateFixture();
        var workspace = CreateWorkspace("虚构测试对象");

        var save = await fixture.Workflow.SaveCurrentAsync(workspace);
        var browse = await fixture.Workflow.BrowseAsync();
        var record = Assert.Single(browse.Records);
        var open = await fixture.Workflow.OpenStoredAsync(record.DocumentId);

        Assert.True(save.IsSuccess);
        Assert.True(browse.Operation.IsSuccess);
        Assert.True(open.Operation.IsSuccess);
        Assert.Equal("虚构测试对象", open.Review?.SubjectDisplay);
        Assert.Equal(record.PatientId, open.Review?.PatientContext?.PatientId);
        var consultation = Assert.Single(open.Review!.Sections, section => section.Title == "咨询概况");
        var consultationStartedAt = Assert.Single(
            consultation.Fields,
            field => field.Label == "咨询开始");
        Assert.NotNull(consultationStartedAt.Instant);
        Assert.Null(consultationStartedAt.Value);
        Assert.NotEmpty(fixture.Store.Documents);
    }

    [Fact]
    public async Task Follow_up_consultation_reuses_patient_identity_but_has_an_independent_document()
    {
        var fixture = CreateFixture();
        var initial = CreateWorkspace("同一虚构患者");
        await fixture.Workflow.SaveCurrentAsync(initial);
        var firstRecord = Assert.Single((await fixture.Workflow.BrowseAsync()).Records);
        var opened = await fixture.Workflow.OpenStoredAsync(firstRecord.DocumentId);
        var patient = Assert.IsType<ArchivePatientContext>(opened.Review?.PatientContext);
        var followUpClient = new ClientInfo
        {
            Name = patient.Name,
            Gender = patient.Gender,
            Age = patient.AgeInYears ?? 25,
            Height = patient.HeightInCentimeters,
            Weight = patient.WeightInKilograms,
            SpecialPhysiologicalPeriod = patient.PhysiologicalState ?? string.Empty
        };
        var followUp = new ConsultationWorkspace(followUpClient, patient);

        var save = await fixture.Workflow.SaveCurrentAsync(followUp);
        var records = (await fixture.Workflow.BrowseAsync()).Records;

        Assert.True(save.IsSuccess);
        Assert.Equal(2, records.Count);
        Assert.Single(records.Select(record => record.PatientId).Distinct());
        Assert.Equal(initial.ContractIdentity.Patient, followUp.ContractIdentity.Patient);
        Assert.NotEqual(initial.ContractIdentity.Consultation, followUp.ContractIdentity.Consultation);
        Assert.NotEqual(firstRecord.DocumentId, followUp.ContractIdentity.Consultation.ResourceId.Value);
        Assert.Equal(30, patient.AgeInYears);
        Assert.Equal(165m, patient.HeightInCentimeters);
        Assert.Equal(55m, patient.WeightInKilograms);
    }

    [Fact]
    public async Task Equal_patient_names_do_not_merge_independent_consultations()
    {
        var fixture = CreateFixture();

        await fixture.Workflow.SaveCurrentAsync(CreateWorkspace("同名虚构患者"));
        await fixture.Workflow.SaveCurrentAsync(CreateWorkspace("同名虚构患者"));
        var records = (await fixture.Workflow.BrowseAsync()).Records;

        Assert.Equal(2, records.Count);
        Assert.Equal(2, records.Select(record => record.PatientId).Distinct().Count());
    }

    [Fact]
    public async Task Repeated_saves_of_one_consultation_replace_its_draft_document()
    {
        var fixture = CreateFixture();
        var workspace = CreateWorkspace("重复保存对象");

        await fixture.Workflow.SaveCurrentAsync(workspace);
        await fixture.Workflow.SaveCurrentAsync(workspace);

        Assert.Single(fixture.Store.Documents);
        Assert.Single((await fixture.Workflow.BrowseAsync()).Records);
    }

    [Fact]
    public async Task Export_uses_an_identity_safe_filename_and_import_builds_a_read_only_review()
    {
        var fixture = CreateFixture();
        var workspace = CreateWorkspace("不应进入文件名的姓名");

        var export = await fixture.Workflow.ExportCurrentAsync(workspace);
        fixture.Transport.NextInput = new ExternalArchiveDocument
        {
            FileName = fixture.Transport.LastExport?.SuggestedFileName,
            MediaType = fixture.Transport.LastExport?.MediaType,
            Content = fixture.Transport.LastExport?.Content ?? ReadOnlyMemory<byte>.Empty
        };
        var import = await fixture.Workflow.ImportAsync();

        Assert.True(export.IsSuccess);
        Assert.NotNull(fixture.Transport.LastExport);
        Assert.DoesNotContain("姓名", fixture.Transport.LastExport.SuggestedFileName, StringComparison.Ordinal);
        Assert.EndsWith(".xml", fixture.Transport.LastExport.SuggestedFileName, StringComparison.Ordinal);
        Assert.True(import.Operation.IsSuccess);
        Assert.Equal("不应进入文件名的姓名", import.Review?.SubjectDisplay);
    }

    private static Fixture CreateFixture()
    {
        var codec = new MemoryCodec();
        var store = new MemoryStore();
        var transport = new MemoryTransport();
        var assembler = new ArchiveContractAssembler(new ApplicationIdentity(
            new Uri("https://example.invalid/tests/archive-workflow"),
            "档案工作流测试",
            "1.0"));
        var workflow = new ArchiveWorkflow(
            assembler,
            new ArchiveContractValidator(),
            [codec],
            store,
            transport);
        return new Fixture(workflow, store, transport);
    }

    private static ConsultationWorkspace CreateWorkspace(string name) => new(new ClientInfo
    {
        Name = name,
        Gender = "女",
        Age = 30,
        Height = 165,
        Weight = 55
    });

    private sealed record Fixture(
        ArchiveWorkflow Workflow,
        MemoryStore Store,
        MemoryTransport Transport);

    private sealed class MemoryCodec : IArchiveCodec
    {
        private static readonly ArchiveFormatDescriptor Format = new(
            new Uri("https://example.invalid/formats/memory-xml"),
            "1.0",
            "application/xml");
        private ArchiveDocument? document;

        public Uri CodecIdentifier { get; } = new("https://example.invalid/codecs/memory");

        public IReadOnlyCollection<ArchiveFormatDescriptor> ReadableFormats { get; } = [Format];

        public IReadOnlyCollection<ArchiveFormatDescriptor> WritableFormats { get; } = [Format];

        public ValueTask<ArchiveReadResult> ReadAsync(
            Stream source,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(new ArchiveReadResult
            {
                Document = document is null ? null : document with { SourceFormat = Format },
                Validation = new ArchiveValidationResult()
            });

        public async ValueTask<ArchiveWriteResult> WriteAsync(
            ArchiveWriteRequest request,
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            document = request.Document;
            await destination.WriteAsync(Encoding.UTF8.GetBytes("<archive />"), cancellationToken);
            return new ArchiveWriteResult
            {
                TargetFormat = request.TargetFormat,
                Validation = new ArchiveValidationResult()
            };
        }
    }

    private sealed class MemoryStore : IArchiveDocumentStore
    {
        public Dictionary<Guid, StoredArchiveDocument> Documents { get; } = [];

        public ArchiveDocumentStoreCapabilities Capabilities =>
            ArchiveDocumentStoreCapabilities.Save | ArchiveDocumentStoreCapabilities.Browse;

        public ValueTask SaveAsync(
            StoredArchiveDocument document,
            CancellationToken cancellationToken = default)
        {
            Documents[document.Info.DocumentId] = document;
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<StoredArchiveDocumentInfo>> ListAsync(
            CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<StoredArchiveDocumentInfo>>(
                Documents.Values.Select(document => document.Info).ToArray());

        public ValueTask<StoredArchiveDocument?> GetAsync(
            Guid documentId,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(
                Documents.GetValueOrDefault(documentId));
    }

    private sealed class MemoryTransport : IArchiveDocumentTransport
    {
        public bool CanOpen => true;

        public bool CanSave => true;

        public ExternalArchiveDocument? NextInput { get; set; }

        public ArchiveDocumentExport? LastExport { get; private set; }

        public ValueTask<ExternalArchiveDocument?> OpenAsync(
            CancellationToken cancellationToken = default) => ValueTask.FromResult(NextInput);

        public ValueTask SaveAsync(
            ArchiveDocumentExport document,
            CancellationToken cancellationToken = default)
        {
            LastExport = document;
            return ValueTask.CompletedTask;
        }
    }
}
