using EzNutrition.Application.Archives;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;

namespace EzNutrition.Application.Consultations;

/// <summary>
/// 发现宿主注册的营养量表，并建立咨询内或独立速查使用的量表运行实例。
/// </summary>
public sealed class NutritionAssessmentApplicationService
{
    private readonly IReadOnlyList<INutritionAssessmentInstrument> instruments;
    private readonly IReadOnlyList<NutritionAssessmentDefinition> definitions;

    /// <summary>
    /// 使用宿主显式注册的量表实现建立服务。
    /// </summary>
    /// <param name="instruments">当前发布版本可用的量表实现。</param>
    public NutritionAssessmentApplicationService(
        IEnumerable<INutritionAssessmentInstrument> instruments)
    {
        ArgumentNullException.ThrowIfNull(instruments);
        var registered = instruments.ToArray();
        foreach (var instrument in registered)
        {
            ValidateDefinition(instrument);
        }

        var duplicate = registered
            .GroupBy(instrument => new
            {
                instrument.Definition.CodeSystem,
                instrument.Definition.Code,
                instrument.Definition.Version
            })
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"量表 {duplicate.Key.Code} {duplicate.Key.Version} 被重复注册。");
        }

        this.instruments = Array.AsReadOnly(registered);
        definitions = Array.AsReadOnly(
            registered.Select(instrument => instrument.Definition).ToArray());
    }

    /// <summary>获取当前宿主注册、可由用户选用的量表目录。</summary>
    public IReadOnlyList<NutritionAssessmentDefinition> Definitions => definitions;

    /// <summary>
    /// 根据宿主注册的量表定义，在工作区中开始一次量表评估。
    /// </summary>
    /// <returns>新建立并已加入当前工作区的量表运行实例。</returns>
    public NutritionAssessmentRun StartRun(
        ConsultationWorkspace workspace,
        NutritionAssessmentDefinition definition,
        DateTimeOffset? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(definition);

        var instrument = FindInstrument(definition);
        if (workspace.NutritionAssessments.Any(run =>
                HasSameIdentity(run.Definition, instrument.Definition)))
        {
            throw new InvalidOperationException("当前咨询已经添加了该量表。");
        }

        var run = CreateRun(
            instrument,
            CreateSubject(workspace.Client),
            createdAt ?? DateTimeOffset.UtcNow);
        workspace.NutritionAssessments.Add(run);
        return run;
    }

    /// <summary>
    /// 建立一个不隶属于咨询工作区的独立量表运行实例。
    /// </summary>
    /// <remarks>
    /// 调用方持有并管理该实例的生命周期；它不会自动加入咨询、SOAP 或档案，
    /// 适用于量表速查等临时计算场景。
    /// </remarks>
    public NutritionAssessmentRun CreateStandaloneRun(
        NutritionAssessmentDefinition definition,
        NutritionAssessmentSubject subject,
        DateTimeOffset? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(subject);

        return CreateRun(
            FindInstrument(definition),
            subject,
            createdAt ?? DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// 从工作区移除一次量表运行及其回答，使其不再参与后续档案快照。
    /// </summary>
    /// <remarks>已经由专业人员确认并加入 SOAP 的文本属于独立记录，不在此处追溯删除。</remarks>
    /// <returns>找到并移除了指定运行实例时返回 <see langword="true" />。</returns>
    public bool RemoveRun(ConsultationWorkspace workspace, Guid runId)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var run = workspace.NutritionAssessments.SingleOrDefault(candidate =>
            candidate.RunId == runId);
        return run is not null && workspace.NutritionAssessments.Remove(run);
    }

    private static bool HasSameIdentity(
        NutritionAssessmentDefinition left,
        NutritionAssessmentDefinition right) =>
        left.CodeSystem == right.CodeSystem
        && string.Equals(left.Code, right.Code, StringComparison.Ordinal)
        && string.Equals(left.Version, right.Version, StringComparison.Ordinal);

    private INutritionAssessmentInstrument FindInstrument(
        NutritionAssessmentDefinition definition) => instruments.SingleOrDefault(candidate =>
            HasSameIdentity(candidate.Definition, definition))
            ?? throw new ArgumentException("指定量表没有在当前宿主中注册。", nameof(definition));

    private static NutritionAssessmentRun CreateRun(
        INutritionAssessmentInstrument instrument,
        NutritionAssessmentSubject subject,
        DateTimeOffset createdAt) => new(
            instrument,
            subject,
            new ArchiveResourceIdentity
            {
                ResourceId = new ResourceId(Guid.NewGuid()),
                VersionId = new ResourceVersionId(Guid.NewGuid())
            },
            createdAt);

    private static NutritionAssessmentSubject CreateSubject(IClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        var age = client.Age
            ?? throw new InvalidOperationException("建立营养量表前必须取得有效年龄。");
        return new NutritionAssessmentSubject
        {
            AgeInYears = age.Years,
            HeightInCentimeters = client.Height,
            WeightInKilograms = client.Weight
        };
    }

    private static void ValidateDefinition(INutritionAssessmentInstrument instrument)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        var definition = instrument.Definition
            ?? throw new InvalidOperationException("量表实现没有提供定义。");
        if (!definition.CodeSystem.IsAbsoluteUri || !definition.DefinitionUri.IsAbsoluteUri)
        {
            throw new InvalidOperationException("量表代码体系和定义地址必须是绝对 URI。");
        }

        if (string.IsNullOrWhiteSpace(definition.Code)
            || string.IsNullOrWhiteSpace(definition.Version)
            || string.IsNullOrWhiteSpace(definition.DisplayName)
            || string.IsNullOrWhiteSpace(definition.Description)
            || definition.Sections.Count == 0)
        {
            throw new InvalidOperationException("量表定义缺少稳定身份、版本、名称或题目分组。");
        }

        var items = definition.Items.ToArray();
        if (definition.Sections.Any(section =>
                string.IsNullOrWhiteSpace(section.Code)
                || string.IsNullOrWhiteSpace(section.Title))
            || definition.Sections.Select(section => section.Code)
                .Distinct(StringComparer.Ordinal).Count() != definition.Sections.Count
            || items.Length == 0
            || items.Any(IsInvalidItem)
            || items.Select(item => item.Code).Distinct(StringComparer.Ordinal).Count() != items.Length
            || items.Any(item => item.Options.Select(option => option.Code)
                .Distinct(StringComparer.Ordinal).Count() != item.Options.Count))
        {
            throw new InvalidOperationException("量表题目或选项定义无效，或存在重复编码。");
        }
    }

    private static bool IsInvalidItem(NutritionAssessmentItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Code) || string.IsNullOrWhiteSpace(item.Prompt))
        {
            return true;
        }

        var invalidOptions = item.Options.Any(option =>
            string.IsNullOrWhiteSpace(option.Code)
            || string.IsNullOrWhiteSpace(option.Display));
        return item.ResponseType switch
        {
            NutritionAssessmentResponseType.SingleChoice or
                NutritionAssessmentResponseType.MultipleChoice =>
                item.Options.Count == 0
                || invalidOptions
                || item.Unit is not null
                || item.MinimumValue is not null
                || item.MaximumValue is not null,
            NutritionAssessmentResponseType.Decimal =>
                item.Options.Count != 0
                || item.MinimumValue is { } minimum
                    && item.MaximumValue is { } maximum
                    && minimum > maximum,
            _ => true
        };
    }
}
