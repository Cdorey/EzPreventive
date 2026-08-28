using EzNutrition.Application.Archives;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;

namespace EzNutrition.Application.Consultations;

/// <summary>
/// 发现宿主注册的营养量表，并为咨询工作区建立通用量表运行实例。
/// </summary>
public sealed class NutritionAssessmentApplicationService
{
    private readonly IReadOnlyList<INutritionAssessmentInstrument> instruments;

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
    }

    /// <summary>获取当前宿主注册的全部量表实现。</summary>
    public IReadOnlyList<INutritionAssessmentInstrument> Instruments => instruments;

    /// <summary>
    /// 确保工作区为每个已注册量表保留一个当前运行实例。
    /// </summary>
    /// <remarks>
    /// 当前版本在一次咨询内为每个量表建立一个实例；集合结构仍保留未来支持重复测量的空间。
    /// </remarks>
    public void EnsureRuns(
        ConsultationWorkspace workspace,
        DateTimeOffset? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var subject = CreateSubject(workspace.Client);
        var startedAt = createdAt ?? DateTimeOffset.UtcNow;

        foreach (var instrument in instruments)
        {
            var definition = instrument.Definition;
            if (workspace.NutritionAssessments.Any(run =>
                    run.Definition.CodeSystem == definition.CodeSystem
                    && string.Equals(run.Definition.Code, definition.Code, StringComparison.Ordinal)
                    && string.Equals(run.Definition.Version, definition.Version, StringComparison.Ordinal)))
            {
                continue;
            }

            workspace.NutritionAssessments.Add(new NutritionAssessmentRun(
                instrument,
                subject,
                new ArchiveResourceIdentity
                {
                    ResourceId = new ResourceId(Guid.NewGuid()),
                    VersionId = new ResourceVersionId(Guid.NewGuid())
                },
                startedAt));
        }
    }

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
            || items.Any(item => string.IsNullOrWhiteSpace(item.Code)
                || string.IsNullOrWhiteSpace(item.Prompt)
                || item.Options.Count == 0
                || item.Options.Any(option => string.IsNullOrWhiteSpace(option.Code)
                    || string.IsNullOrWhiteSpace(option.Display)))
            || items.Select(item => item.Code).Distinct(StringComparer.Ordinal).Count() != items.Length
            || items.Any(item => item.Options.Select(option => option.Code)
                .Distinct(StringComparer.Ordinal).Count() != item.Options.Count))
        {
            throw new InvalidOperationException("量表题目或选项定义无效，或存在重复编码。");
        }
    }
}
