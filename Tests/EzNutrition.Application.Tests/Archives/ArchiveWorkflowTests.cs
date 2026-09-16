using System.Text;
using EzNutrition.Assessments.Common;
using EzNutrition.Application.Archives;
using EzNutrition.Application.Consultations;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.Validation;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Domain.Consultations;
using DomainChronologicalAge = EzNutrition.Domain.Consultations.ChronologicalAge;

namespace EzNutrition.Application.Tests.Archives;

public sealed class ArchiveWorkflowTests
{
    /// <summary>验证多期历史按项目保留实值，加载前没有读取，重复入口共享同一轮结果。</summary>
    [Fact]
    public async Task Follow_up_history_is_lazy_shared_and_omits_missing_projects()
    {
        var fixture = CreateFixture();
        var first = CreateWorkspace("多期历史对象");
        first.Client.Weight = null;
        Assert.Null(first.History);
        Assert.True((await fixture.Workflow.SaveCurrentAsync(first)).IsSuccess);
        var opened = await fixture.Workflow.OpenStoredAsync(first.ContractIdentity.Consultation.ResourceId.Value);
        var patient = opened.Review!.PatientContext!;
        var followUp = new ConsultationWorkspace(new ClientInfo { Name = patient.Name }, patient);
        var history = Assert.IsType<ConsultationHistory>(followUp.History);
        Assert.False(history.IsLoaded);

        for (var i = 2; i <= 4; i++)
        {
            var previous = new ConsultationWorkspace(new ClientInfo
            {
                Name = patient.Name, Height = 160 + i, Weight = i == 3 ? null : 50 + i
            }, patient);
            if (i == 4)
                previous.SubjectiveObjectiveAssessmentPlanInformation = new() { Plan = "第四次才记录的计划" };
            Assert.True((await fixture.Workflow.SaveCurrentAsync(previous)).IsSuccess);
        }

        var readCount = fixture.Codec.ReadCount;
        var load = history.LoadAsync(fixture.Workflow, CancellationToken.None);
        Assert.Same(load, history.LoadAsync(fixture.Workflow, CancellationToken.None));
        Assert.True(history.IsLoading);
        await load;
        Assert.False(history.IsLoading);
        Assert.True(history.IsLoaded);
        Assert.Equal(4, history.Entries.Count);
        Assert.Equal(readCount + 4, fixture.Codec.ReadCount);
        var facts = history.Entries.SelectMany(entry => entry.Facts).ToArray();
        Assert.Equal(4, facts.Count(fact => fact.Item == ConsultationHistoryItem.Height));
        Assert.Equal(2, facts.Count(fact => fact.Item == ConsultationHistoryItem.Weight));
        Assert.Equal("第四次才记录的计划", Assert.Single(facts, fact => fact.Item == ConsultationHistoryItem.Plan).Text);
        Assert.DoesNotContain(facts, fact => fact.Item == ConsultationHistoryItem.Subjective);
        Assert.Equal(history.Entries.OrderByDescending(entry => entry.ConsultationStartedAt), history.Entries);

        await history.LoadAsync(fixture.Workflow, CancellationToken.None);
        Assert.Equal(readCount + 4, fixture.Codec.ReadCount);
        // 保存新的复诊不包含读取出的既往计划，也不把历史对象写入契约。
        Assert.True((await fixture.Workflow.SaveCurrentAsync(followUp)).IsSuccess);
        var current = await fixture.Workflow.ReadHistoryAsync(patient.PatientId, followUp.ContractIdentity.Consultation.ResourceId.Value);
        Assert.DoesNotContain(current.Entry!.Facts, fact => fact.Item == ConsultationHistoryItem.Plan);
    }

    /// <summary>摘要被错误归组时必须核对正文身份，且单份失败不丢失其他历史。</summary>
    [Fact]
    public async Task History_rejects_foreign_patient_content_and_keeps_valid_records()
    {
        var fixture = CreateFixture();
        var own = CreateWorkspace("同名患者");
        var other = CreateWorkspace("同名患者");
        await fixture.Workflow.SaveCurrentAsync(own);
        await fixture.Workflow.SaveCurrentAsync(other);
        var foreignId = other.ContractIdentity.Consultation.ResourceId.Value;
        var foreign = fixture.Store.Documents[foreignId];
        fixture.Store.Documents[foreignId] = foreign with { Info = foreign.Info with { PatientId = own.ContractIdentity.Patient.ResourceId.Value } };

        var history = new ConsultationHistory(own.ContractIdentity.Patient.ResourceId.Value, Guid.NewGuid());
        await history.LoadAsync(fixture.Workflow, CancellationToken.None);

        Assert.Equal(1, history.FailedCount);
        Assert.Equal(own.ContractIdentity.Consultation.ResourceId.Value, Assert.Single(history.Entries).ConsultationId);
        fixture.Store.Documents.Remove(foreignId);
        await history.ReloadAsync(fixture.Workflow, CancellationToken.None);
        Assert.Equal(0, history.FailedCount);
    }

    /// <summary>取消不留下永久加载状态；重试后排除已保存的本次咨询。</summary>
    [Fact]
    public async Task Cancelled_history_can_retry_and_does_not_include_current_consultation()
    {
        var fixture = CreateFixture();
        var current = CreateWorkspace("取消测试对象");
        await fixture.Workflow.SaveCurrentAsync(current);
        var history = new ConsultationHistory(current.ContractIdentity.Patient.ResourceId.Value, current.ContractIdentity.Consultation.ResourceId.Value);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => history.LoadAsync(fixture.Workflow, cancellation.Token));
        Assert.False(history.IsLoading);
        Assert.False(history.IsLoaded);
        await history.LoadAsync(fixture.Workflow, CancellationToken.None);
        Assert.True(history.IsLoaded);
        Assert.Empty(history.Entries);
    }

    /// <summary>项目时间缺失时明确回退到咨询时间，读取不依赖文档标识等于咨询标识。</summary>
    [Fact]
    public async Task History_keeps_time_provenance_and_accepts_provider_document_identifiers()
    {
        var fixture = CreateFixture();
        var workspace = CreateWorkspace("时间测试对象");
        await fixture.Workflow.SaveCurrentAsync(workspace);
        var stored = Assert.Single(fixture.Store.Documents).Value;
        var document = fixture.Codec.Documents.Values.Single();
        var consultation = document.Bundle.Entries.OfType<ConsultationResource>().Single();
        var changed = consultation with
        {
            SubjectSnapshot = consultation.SubjectSnapshot! with
            {
                Weight = consultation.SubjectSnapshot!.Weight! with { EffectiveAt = null }
            }
        };
        fixture.Codec.Documents[document.Bundle.BundleId.Value] = document with
        {
            Bundle = document.Bundle with { Entries = document.Bundle.Entries.Select(resource => ReferenceEquals(resource, consultation) ? changed : resource).ToArray() }
        };
        var providerId = Guid.NewGuid();
        fixture.Store.Documents[providerId] = stored with { Info = stored.Info with { DocumentId = providerId } };

        var result = await fixture.Workflow.ReadHistoryAsync(workspace.ContractIdentity.Patient.ResourceId.Value, providerId);
        Assert.True(result.Operation.IsSuccess);
        var weight = Assert.Single(result.Entry!.Facts, fact => fact.Item == ConsultationHistoryItem.Weight);
        Assert.True(weight.UsesConsultationTime);
        Assert.Equal(consultation.Period.Start, weight.EffectiveAt);
        Assert.Equal(55m, weight.Quantity!.Value);
        Assert.Equal("kg", weight.Quantity.UnitCode);
        Assert.False(Assert.Single(result.Entry.Facts, fact => fact.Item == ConsultationHistoryItem.Height).UsesConsultationTime);
    }

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
    public async Task Scale_review_projects_the_archived_answers_and_results_as_read_only_details()
    {
        var fixture = CreateFixture();
        var workspace = CreateWorkspace("量表详情测试对象");
        var assessments = new NutritionAssessmentApplicationService([new Nrs2002Instrument()]);
        var run = assessments.StartRun(
            workspace,
            Assert.Single(assessments.Definitions),
            workspace.ContractIdentity.CreatedAt);
        run.SetAnswer("bmi-status", "bmi-at-least-18-5");
        run.SetAnswer("recent-weight-loss", "over-five-percent-within-three-months");
        run.SetAnswer("last-week-intake-reduction", "no-scored-intake-reduction");
        run.SetAnswer("disease-severity", "mild");

        await fixture.Workflow.SaveCurrentAsync(workspace);
        var record = Assert.Single((await fixture.Workflow.BrowseAsync()).Records);
        var opened = await fixture.Workflow.OpenStoredAsync(record.DocumentId);

        var scale = Assert.Single(
            opened.Review!.Sections,
            section => section.Title == "临床营养风险筛查 NRS 2002");
        var responses = Assert.Single(scale.DetailGroups, group => group.Title == "逐题作答");
        var weightLoss = Assert.Single(
            responses.Fields,
            field => field.Label == "近期（1 个月～3 个月）体重是否下降？");
        var derivedResults = Assert.Single(scale.DetailGroups, group => group.Title == "派生结果");
        var nutritionalStatus = Assert.Single(
            derivedResults.Fields,
            field => field.Label == "营养状况受损评分");
        var conclusion = Assert.Single(scale.DetailGroups, group => group.Title == "评估结论");

        Assert.Equal(4, responses.Fields.Count);
        Assert.Equal("近 3 个月内体重下降 >5%（本题 1 分）", weightLoss.Value);
        Assert.Equal("1", nutritionalStatus.Value);
        Assert.Equal("2 分", Assert.Single(conclusion.Fields, field => field.Label == "总分").Value);
        Assert.Equal(
            "目前没有营养风险",
            Assert.Single(conclusion.Fields, field => field.Label == "结果解释").Value);
    }

    [Fact]
    public async Task Birth_date_and_structured_age_are_available_after_opening_an_archive()
    {
        var fixture = CreateFixture();
        var workspace = CreateWorkspace("合成儿保对象");
        workspace.Client.BirthDate = new DateOnly(2024, 4, 17);
        workspace.Client.Age = new DomainChronologicalAge(1, 4, 23);

        await fixture.Workflow.SaveCurrentAsync(workspace);
        var record = Assert.Single((await fixture.Workflow.BrowseAsync()).Records);
        var opened = await fixture.Workflow.OpenStoredAsync(record.DocumentId);

        var patient = Assert.IsType<ArchivePatientContext>(opened.Review?.PatientContext);
        Assert.Equal(new DateOnly(2024, 4, 17), patient.BirthDate);
        Assert.Equal(new DomainChronologicalAge(1, 4, 23), patient.Age);
        Assert.Equal(1, patient.AgeInYears);
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
            Age = patient.Age ?? new DomainChronologicalAge(25),
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
    public async Task Delete_and_clear_are_optional_store_capabilities()
    {
        var fixture = CreateFixture();
        await fixture.Workflow.SaveCurrentAsync(CreateWorkspace("待删除对象一"));
        await fixture.Workflow.SaveCurrentAsync(CreateWorkspace("待删除对象二"));
        var records = (await fixture.Workflow.BrowseAsync()).Records;

        var delete = await fixture.Workflow.DeleteStoredAsync(records[0].DocumentId);
        var afterDelete = (await fixture.Workflow.BrowseAsync()).Records;
        var clear = await fixture.Workflow.ClearStoredAsync();

        Assert.True(fixture.Workflow.Capabilities.HasFlag(ArchiveWorkflowCapabilities.Delete));
        Assert.True(fixture.Workflow.Capabilities.HasFlag(ArchiveWorkflowCapabilities.Clear));
        Assert.True(delete.IsSuccess);
        Assert.Single(afterDelete);
        Assert.True(clear.IsSuccess);
        Assert.Empty(fixture.Store.Documents);
    }

    [Fact]
    public async Task Mutation_operations_distinguish_unavailable_and_denied()
    {
        var unavailable = CreateFixture(ArchiveDocumentStoreCapabilities.Browse);
        var denied = CreateFixture(denyMutations: true);

        var unavailableDelete = await unavailable.Workflow.DeleteStoredAsync(Guid.NewGuid());
        var unavailableClear = await unavailable.Workflow.ClearStoredAsync();
        var deniedDelete = await denied.Workflow.DeleteStoredAsync(Guid.NewGuid());
        var deniedClear = await denied.Workflow.ClearStoredAsync();

        Assert.Equal(ArchiveOperationStatus.Unavailable, unavailableDelete.Status);
        Assert.Equal(ArchiveOperationStatus.Unavailable, unavailableClear.Status);
        Assert.Equal(ArchiveOperationStatus.Denied, deniedDelete.Status);
        Assert.Equal(ArchiveOperationStatus.Denied, deniedClear.Status);
    }

    [Fact]
    public async Task Export_uses_an_identity_safe_filename_and_import_builds_a_read_only_review()
    {
        var fixture = CreateFixture();
        var workspace = CreateWorkspace("不应进入文件名的姓名");

        var export = await fixture.Workflow.ExportCurrentAsync(workspace);
        fixture.Transport.NextInput = new ExternalArchiveDocument
        {
            FileName = fixture.Transport.LastExport is { } exported
                ? exported.SuggestedFileNameStem + exported.Format.PreferredFileExtension
                : null,
            MediaType = fixture.Transport.LastExport?.Format.MediaType,
            Content = fixture.Transport.LastExport?.Content ?? ReadOnlyMemory<byte>.Empty
        };
        var import = await fixture.Workflow.ImportAsync();

        Assert.True(export.IsSuccess);
        Assert.NotNull(fixture.Transport.LastExport);
        Assert.DoesNotContain("姓名", fixture.Transport.LastExport.SuggestedFileNameStem, StringComparison.Ordinal);
        Assert.Equal(".archive-test", fixture.Transport.LastExport.Format.PreferredFileExtension);
        Assert.True(import.Operation.IsSuccess);
        Assert.Equal("不应进入文件名的姓名", import.Review?.SubjectDisplay);
    }

    [Fact]
    public async Task Stored_export_forwards_the_saved_document_without_reencoding_it()
    {
        var fixture = CreateFixture();
        var workspace = CreateWorkspace("已保存档案导出对象");
        await fixture.Workflow.SaveCurrentAsync(workspace);
        var stored = Assert.Single(fixture.Store.Documents).Value;
        stored = stored with
        {
            Info = stored.Info with
            {
                FormatDisplayName = null,
                PreferredFileExtension = null
            }
        };
        fixture.Store.Documents[stored.Info.DocumentId] = stored;
        var writesBeforeExport = fixture.Codec.WriteCount;

        var export = await fixture.Workflow.ExportStoredAsync(stored.Info.DocumentId);

        Assert.True(export.IsSuccess);
        Assert.True(fixture.Workflow.Capabilities.HasFlag(ArchiveWorkflowCapabilities.ExportStored));
        Assert.Equal(writesBeforeExport, fixture.Codec.WriteCount);
        Assert.Equal(stored.Content, fixture.Transport.LastExport?.Content);
        Assert.Equal(stored.Info.FormatIdentifier, fixture.Transport.LastExport?.Format.Identifier.AbsoluteUri);
        Assert.Equal(stored.Info.FormatVersion, fixture.Transport.LastExport?.Format.Version);
        Assert.Equal("测试档案格式", fixture.Transport.LastExport?.Format.DisplayName);
        Assert.Equal(".archive-test", fixture.Transport.LastExport?.Format.PreferredFileExtension);
        Assert.Equal($"eznutrition-{stored.Info.DocumentId:N}", fixture.Transport.LastExport?.SuggestedFileNameStem);
    }

    [Fact]
    public async Task Stored_export_capability_and_result_follow_host_policy()
    {
        var unavailable = CreateFixture(canSaveExternal: false);
        var denied = CreateFixture(denyExternalSave: true);
        var deniedWorkspace = CreateWorkspace("导出策略拒绝对象");
        await denied.Workflow.SaveCurrentAsync(deniedWorkspace);
        var deniedDocumentId = Assert.Single(denied.Store.Documents).Key;

        var unavailableResult = await unavailable.Workflow.ExportStoredAsync(Guid.NewGuid());
        var deniedResult = await denied.Workflow.ExportStoredAsync(deniedDocumentId);

        Assert.False(unavailable.Workflow.Capabilities.HasFlag(ArchiveWorkflowCapabilities.ExportStored));
        Assert.Equal(ArchiveOperationStatus.Unavailable, unavailableResult.Status);
        Assert.Equal(ArchiveOperationStatus.Denied, deniedResult.Status);
    }

    [Fact]
    public async Task Export_reports_user_cancellation_without_treating_it_as_a_failure()
    {
        var fixture = CreateFixture(cancelExternalSave: true);
        var current = await fixture.Workflow.ExportCurrentAsync(CreateWorkspace("取消导出对象"));
        await fixture.Workflow.SaveCurrentAsync(CreateWorkspace("取消已保存档案导出对象"));
        var storedId = Assert.Single(fixture.Store.Documents).Key;
        var stored = await fixture.Workflow.ExportStoredAsync(storedId);

        Assert.Equal(ArchiveOperationStatus.Cancelled, current.Status);
        Assert.Equal(ArchiveOperationStatus.Cancelled, stored.Status);
    }

    /// <summary>
    /// 验证同步占用 CPU 的 codec 不会阻塞发起档案保存的调用线程。
    /// </summary>
    [Fact]
    public async Task SaveCurrentAsync_dispatches_encoding_before_host_storage()
    {
        var fixture = CreateFixture();
        using var writeStarted = new ManualResetEventSlim();
        using var continueWrite = new ManualResetEventSlim();
        using var invocationReturned = new ManualResetEventSlim();
        fixture.Codec.WriteStarted = writeStarted;
        fixture.Codec.ContinueWrite = continueWrite;
        Task<ArchiveOperationResult>? operation = null;
        Exception? invocationException = null;
        var caller = new Thread(() =>
        {
            try
            {
                operation = fixture.Workflow.SaveCurrentAsync(CreateWorkspace("后台编码对象")).AsTask();
            }
            catch (Exception exception)
            {
                invocationException = exception;
            }
            finally
            {
                invocationReturned.Set();
            }
        });

        caller.Start();
        try
        {
            Assert.True(invocationReturned.Wait(TimeSpan.FromSeconds(5)));
            Assert.Null(invocationException);
            Assert.True(writeStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.NotNull(operation);
            Assert.False(operation.IsCompleted);

            continueWrite.Set();
            var result = await operation.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(result.IsSuccess);
            Assert.NotEmpty(fixture.Store.Documents);
        }
        finally
        {
            continueWrite.Set();
            Assert.True(caller.Join(TimeSpan.FromSeconds(5)));
        }
    }

    private static Fixture CreateFixture(
        ArchiveDocumentStoreCapabilities capabilities =
            ArchiveDocumentStoreCapabilities.Save |
            ArchiveDocumentStoreCapabilities.Browse |
            ArchiveDocumentStoreCapabilities.Delete |
            ArchiveDocumentStoreCapabilities.Clear,
        bool denyMutations = false,
        bool canSaveExternal = true,
        bool denyExternalSave = false,
        bool cancelExternalSave = false)
    {
        var codec = new MemoryCodec();
        var store = new MemoryStore(capabilities) { DenyMutations = denyMutations };
        var transport = new MemoryTransport
        {
            CanSave = canSaveExternal,
            DenySave = denyExternalSave,
            CancelSave = cancelExternalSave
        };
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
        return new Fixture(workflow, codec, store, transport);
    }

    private static ConsultationWorkspace CreateWorkspace(string name) => new(new ClientInfo
    {
        Name = name,
        Gender = "女",
        Age = new DomainChronologicalAge(30),
        Height = 165,
        Weight = 55
    });

    private sealed record Fixture(
        ArchiveWorkflow Workflow,
        MemoryCodec Codec,
        MemoryStore Store,
        MemoryTransport Transport);

    private sealed class MemoryCodec : IArchiveCodec
    {
        private static readonly ArchiveFormatDescriptor Format = new(
            new Uri("https://example.invalid/formats/memory-xml"),
            "1.0",
            "application/x-archive-test",
            "测试档案格式",
            ".archive-test");
        public Dictionary<Guid, ArchiveDocument> Documents { get; } = [];
        public int ReadCount { get; private set; }

        public Uri CodecIdentifier { get; } = new("https://example.invalid/codecs/memory");

        public IReadOnlyCollection<ArchiveFormatDescriptor> ReadableFormats { get; } = [Format];

        public IReadOnlyCollection<ArchiveFormatDescriptor> WritableFormats { get; } = [Format];

        public ManualResetEventSlim? WriteStarted { get; set; }

        public ManualResetEventSlim? ContinueWrite { get; set; }

        public int WriteCount { get; private set; }

        public async ValueTask<ArchiveReadResult> ReadAsync(
            Stream source,
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            using var reader = new StreamReader(source, leaveOpen: true);
            var key = await reader.ReadToEndAsync(cancellationToken);
            var document = Guid.TryParse(key, out var id) ? Documents.GetValueOrDefault(id) : null;
            return new ArchiveReadResult
            {
                Document = document is null ? null : document with { SourceFormat = Format },
                Validation = new ArchiveValidationResult()
            };
        }

        public async ValueTask<ArchiveWriteResult> WriteAsync(
            ArchiveWriteRequest request,
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            WriteCount++;
            WriteStarted?.Set();
            ContinueWrite?.Wait(cancellationToken);
            Documents[request.Document.Bundle.BundleId.Value] = request.Document;
            await destination.WriteAsync(Encoding.UTF8.GetBytes(request.Document.Bundle.BundleId.Value.ToString("D")), cancellationToken);
            return new ArchiveWriteResult
            {
                TargetFormat = request.TargetFormat,
                Validation = new ArchiveValidationResult()
            };
        }
    }

    private sealed class MemoryStore(ArchiveDocumentStoreCapabilities capabilities) : IArchiveDocumentStore
    {
        public Dictionary<Guid, StoredArchiveDocument> Documents { get; } = [];

        public ArchiveDocumentStoreCapabilities Capabilities { get; } = capabilities;

        public bool DenyMutations { get; init; }

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

        public ValueTask DeleteAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            ThrowIfMutationsDenied();
            Documents.Remove(documentId);
            return ValueTask.CompletedTask;
        }

        public ValueTask ClearAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfMutationsDenied();
            Documents.Clear();
            return ValueTask.CompletedTask;
        }

        private void ThrowIfMutationsDenied()
        {
            if (DenyMutations)
            {
                throw new UnauthorizedAccessException();
            }
        }
    }

    private sealed class MemoryTransport : IArchiveDocumentTransport
    {
        public bool CanOpen => true;

        public bool CanSave { get; init; } = true;

        public bool DenySave { get; init; }

        public bool CancelSave { get; init; }

        public ExternalArchiveDocument? NextInput { get; set; }

        public ArchiveDocumentExport? LastExport { get; private set; }

        public ValueTask<ExternalArchiveDocument?> OpenAsync(
            CancellationToken cancellationToken = default) => ValueTask.FromResult(NextInput);

        public ValueTask<bool> SaveAsync(
            ArchiveDocumentExport document,
            CancellationToken cancellationToken = default)
        {
            if (DenySave)
            {
                throw new UnauthorizedAccessException();
            }

            LastExport = document;
            return ValueTask.FromResult(!CancelSave);
        }
    }
}
