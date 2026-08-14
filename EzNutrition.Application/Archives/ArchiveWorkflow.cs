using EzNutrition.Application.Consultations;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.Validation;

namespace EzNutrition.Application.Archives;

/// <summary>
/// 编排运行态咨询、档案契约、编码格式与宿主存储之间的格式无关用例。
/// </summary>
public sealed class ArchiveWorkflow : IArchiveWorkflow
{
    private readonly ArchiveContractAssembler assembler;
    private readonly IArchiveValidator validator;
    private readonly IReadOnlyList<IArchiveCodec> codecs;
    private readonly IArchiveDocumentStore store;
    private readonly IArchiveDocumentTransport transport;

    /// <summary>
    /// 初始化档案工作流。
    /// </summary>
    public ArchiveWorkflow(
        ArchiveContractAssembler assembler,
        IArchiveValidator validator,
        IEnumerable<IArchiveCodec> codecs,
        IArchiveDocumentStore store,
        IArchiveDocumentTransport transport)
    {
        ArgumentNullException.ThrowIfNull(assembler);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(codecs);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(transport);

        this.assembler = assembler;
        this.validator = validator;
        this.codecs = codecs.OrderBy(codec => codec.CodecIdentifier.AbsoluteUri, StringComparer.Ordinal).ToArray();
        this.store = store;
        this.transport = transport;
    }

    /// <inheritdoc />
    public ArchiveWorkflowCapabilities Capabilities
    {
        get
        {
            var capabilities = ArchiveWorkflowCapabilities.None;
            if (HasWritableCodec && store.Capabilities.HasFlag(ArchiveDocumentStoreCapabilities.Save))
            {
                capabilities |= ArchiveWorkflowCapabilities.Save;
            }

            if (HasReadableCodec && store.Capabilities.HasFlag(ArchiveDocumentStoreCapabilities.Browse))
            {
                capabilities |= ArchiveWorkflowCapabilities.Browse;
            }

            if (HasReadableCodec && transport.CanOpen)
            {
                capabilities |= ArchiveWorkflowCapabilities.Import;
            }

            if (HasWritableCodec && transport.CanSave)
            {
                capabilities |= ArchiveWorkflowCapabilities.Export;
            }

            return capabilities;
        }
    }

    /// <inheritdoc />
    public async ValueTask<ArchiveOperationResult> SaveCurrentAsync(
        ConsultationWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        if (!Capabilities.HasFlag(ArchiveWorkflowCapabilities.Save))
        {
            return Unavailable("当前运行环境没有配置本机档案保存能力。");
        }

        try
        {
            var encoded = await EncodeAsync(workspace, ArchiveValidationScope.DraftSave, cancellationToken);
            if (encoded.Operation is not null)
            {
                return encoded.Operation;
            }

            var document = encoded.Document!;
            var consultation = document.Bundle.Entries.OfType<ConsultationResource>().Single();
            var patient = document.Bundle.Entries.OfType<PatientResource>().SingleOrDefault();
            var targetFormat = encoded.TargetFormat!;
            var subject = ArchiveReviewProjector.PatientDisplay(patient, consultation);
            var info = new StoredArchiveDocumentInfo
            {
                DocumentId = consultation.Metadata.ResourceId.Value,
                PatientId = patient?.Metadata.ResourceId.Value,
                Title = consultation.Title ?? $"{subject}的营养咨询",
                SubjectDisplay = subject,
                ConsultationStartedAt = consultation.Period.Start,
                LastSavedAt = DateTimeOffset.UtcNow,
                FormatIdentifier = targetFormat.Identifier.AbsoluteUri,
                FormatVersion = targetFormat.Version,
                MediaType = targetFormat.MediaType ?? "application/octet-stream"
            };

            await store.SaveAsync(new StoredArchiveDocument
            {
                Info = info,
                Content = encoded.Content
            }, cancellationToken);

            return Success("档案已保存到本机档案库。", encoded.Notices);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedHostFailure(exception))
        {
            return Failed("保存档案失败，请检查本机存储是否可用。");
        }
    }

    /// <inheritdoc />
    public async ValueTask<ArchiveBrowseResult> BrowseAsync(CancellationToken cancellationToken = default)
    {
        if (!Capabilities.HasFlag(ArchiveWorkflowCapabilities.Browse))
        {
            return new ArchiveBrowseResult { Operation = Unavailable("当前运行环境没有配置档案调阅能力。") };
        }

        try
        {
            var records = await store.ListAsync(cancellationToken);
            return new ArchiveBrowseResult
            {
                Operation = Success(records.Count == 0 ? "本机档案库目前为空。" : "已加载本机档案。"),
                Records = records
                    .OrderByDescending(record => record.LastSavedAt)
                    .Select(ArchiveReviewProjector.CreateSummary)
                    .ToArray()
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedHostFailure(exception))
        {
            return new ArchiveBrowseResult { Operation = Failed("读取本机档案列表失败。") };
        }
    }

    /// <inheritdoc />
    public async ValueTask<ArchiveOpenResult> OpenStoredAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        if (!Capabilities.HasFlag(ArchiveWorkflowCapabilities.Browse))
        {
            return new ArchiveOpenResult { Operation = Unavailable("当前运行环境没有配置档案调阅能力。") };
        }

        try
        {
            var stored = await store.GetAsync(documentId, cancellationToken);
            if (stored is null)
            {
                return new ArchiveOpenResult { Operation = Failed("没有找到指定档案，它可能已被其他窗口移除。") };
            }

            return await DecodeAsync(
                stored.Content,
                stored.Info.MediaType,
                stored.Info.FormatIdentifier,
                stored.Info.FormatVersion,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedHostFailure(exception))
        {
            return new ArchiveOpenResult { Operation = Failed("读取本机档案失败。") };
        }
    }

    /// <inheritdoc />
    public async ValueTask<ArchiveOpenResult> ImportAsync(CancellationToken cancellationToken = default)
    {
        if (!Capabilities.HasFlag(ArchiveWorkflowCapabilities.Import))
        {
            return new ArchiveOpenResult { Operation = Unavailable("当前运行环境不能打开外部档案文档。") };
        }

        try
        {
            var external = await transport.OpenAsync(cancellationToken);
            if (external is null)
            {
                return new ArchiveOpenResult { Operation = Cancelled("已取消打开档案。") };
            }

            return await DecodeAsync(external.Content, external.MediaType, null, null, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedHostFailure(exception))
        {
            return new ArchiveOpenResult { Operation = Failed("打开外部档案失败。") };
        }
    }

    /// <inheritdoc />
    public async ValueTask<ArchiveOperationResult> ExportCurrentAsync(
        ConsultationWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        if (!Capabilities.HasFlag(ArchiveWorkflowCapabilities.Export))
        {
            return Unavailable("当前运行环境不能导出档案文档。");
        }

        try
        {
            var encoded = await EncodeAsync(workspace, ArchiveValidationScope.Export, cancellationToken);
            if (encoded.Operation is not null)
            {
                return encoded.Operation;
            }

            var consultation = encoded.Document!.Bundle.Entries.OfType<ConsultationResource>().Single();
            await transport.SaveAsync(new ArchiveDocumentExport
            {
                SuggestedFileName = $"eznutrition-{consultation.Metadata.ResourceId.Value:N}.xml",
                MediaType = encoded.TargetFormat!.MediaType ?? "application/octet-stream",
                Content = encoded.Content
            }, cancellationToken);

            return Success("档案文档已导出。", encoded.Notices);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedHostFailure(exception))
        {
            return Failed("导出档案失败，请重试。");
        }
    }

    private bool HasWritableCodec => codecs.Any(codec => codec.WritableFormats.Count > 0);

    private bool HasReadableCodec => codecs.Any(codec => codec.ReadableFormats.Count > 0);

    private async ValueTask<EncodedArchive> EncodeAsync(
        ConsultationWorkspace workspace,
        ArchiveValidationScope scope,
        CancellationToken cancellationToken)
    {
        var document = assembler.CreateDocument(workspace);
        var semanticValidation = validator.ValidateBundle(document.Bundle, scope);
        if (semanticValidation.HasErrors)
        {
            return EncodedArchive.Invalid(Invalid("当前咨询未通过档案校验，尚未写出。", semanticValidation));
        }

        var choice = codecs
            .SelectMany(codec => codec.WritableFormats.Select(format => (Codec: codec, Format: format)))
            .OrderBy(candidate => candidate.Format.Identifier.AbsoluteUri, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Format.Version, StringComparer.Ordinal)
            .FirstOrDefault();
        if (choice.Codec is null || choice.Format is null)
        {
            return EncodedArchive.Invalid(Unavailable("没有配置可写出的档案格式。"));
        }

        await using var destination = new MemoryStream();
        var writeResult = await choice.Codec.WriteAsync(new ArchiveWriteRequest
        {
            Document = document,
            TargetFormat = choice.Format
        }, destination, cancellationToken);
        if (!writeResult.IsSuccess)
        {
            return EncodedArchive.Invalid(Invalid("档案编码失败，未产生可保存的文档。", writeResult.Validation));
        }

        return new EncodedArchive
        {
            Document = document,
            TargetFormat = choice.Format,
            Content = destination.ToArray(),
            Notices = ToNotices(semanticValidation)
                .Concat(ToNotices(writeResult.Validation))
                .DistinctBy(notice => (notice.Code, notice.Message))
                .ToArray()
        };
    }

    private async ValueTask<ArchiveOpenResult> DecodeAsync(
        ReadOnlyMemory<byte> content,
        string? mediaType,
        string? formatIdentifier,
        string? formatVersion,
        CancellationToken cancellationToken)
    {
        var codec = SelectReadableCodec(mediaType, formatIdentifier, formatVersion);
        if (codec is null)
        {
            return new ArchiveOpenResult { Operation = Invalid("无法识别该档案文档的格式。") };
        }

        await using var source = new MemoryStream(content.ToArray(), writable: false);
        var readResult = await codec.ReadAsync(source, cancellationToken);
        if (!readResult.IsSuccess || readResult.Document is null)
        {
            return new ArchiveOpenResult
            {
                Operation = Invalid("档案文档未通过格式或语义校验。", readResult.Validation)
            };
        }

        return new ArchiveOpenResult
        {
            Operation = Success("档案已安全打开。", ToNotices(readResult.Validation)),
            Review = ArchiveReviewProjector.Create(readResult.Document)
        };
    }

    private IArchiveCodec? SelectReadableCodec(
        string? mediaType,
        string? formatIdentifier,
        string? formatVersion)
    {
        if (!string.IsNullOrWhiteSpace(formatIdentifier) && !string.IsNullOrWhiteSpace(formatVersion))
        {
            var exact = codecs.FirstOrDefault(codec => codec.ReadableFormats.Any(format =>
                string.Equals(format.Identifier.AbsoluteUri, formatIdentifier, StringComparison.Ordinal) &&
                string.Equals(format.Version, formatVersion, StringComparison.Ordinal)));
            if (exact is not null)
            {
                return exact;
            }
        }

        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            var byMediaType = codecs.Where(codec => codec.ReadableFormats.Any(format =>
                string.Equals(format.MediaType, mediaType, StringComparison.OrdinalIgnoreCase))).ToArray();
            if (byMediaType.Length == 1)
            {
                return byMediaType[0];
            }
        }

        var readable = codecs.Where(codec => codec.ReadableFormats.Count > 0).ToArray();
        return readable.Length == 1 ? readable[0] : null;
    }

    private static ArchiveOperationResult Success(
        string message,
        IEnumerable<ArchiveNotice>? notices = null) => new()
        {
            Status = ArchiveOperationStatus.Succeeded,
            Message = message,
            Notices = notices?.ToArray() ?? []
        };

    private static ArchiveOperationResult Cancelled(string message) => new()
    {
        Status = ArchiveOperationStatus.Cancelled,
        Message = message
    };

    private static ArchiveOperationResult Unavailable(string message) => new()
    {
        Status = ArchiveOperationStatus.Unavailable,
        Message = message
    };

    private static ArchiveOperationResult Invalid(
        string message,
        ArchiveValidationResult? validation = null) => new()
        {
            Status = ArchiveOperationStatus.Invalid,
            Message = message,
            Notices = validation is null ? [] : ToNotices(validation)
        };

    private static ArchiveOperationResult Failed(string message) => new()
    {
        Status = ArchiveOperationStatus.Failed,
        Message = message
    };

    private static ArchiveNotice[] ToNotices(ArchiveValidationResult validation) => validation.Issues
        .Select(issue => new ArchiveNotice
        {
            Code = issue.Code,
            IsBlocking = issue.Severity is ArchiveValidationSeverity.Error or ArchiveValidationSeverity.Fatal,
            Message = issue.Message
        })
        .ToArray();

    private static bool IsExpectedHostFailure(Exception exception) => exception is
        IOException or
        InvalidDataException or
        InvalidOperationException or
        FormatException;

    private sealed record EncodedArchive
    {
        public ArchiveDocument? Document { get; init; }

        public ArchiveFormatDescriptor? TargetFormat { get; init; }

        public ReadOnlyMemory<byte> Content { get; init; }

        public IReadOnlyList<ArchiveNotice> Notices { get; init; } = Array.Empty<ArchiveNotice>();

        public ArchiveOperationResult? Operation { get; init; }

        public static EncodedArchive Invalid(ArchiveOperationResult operation) => new() { Operation = operation };
    }
}
