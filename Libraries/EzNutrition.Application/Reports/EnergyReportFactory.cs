using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Application.Archives;
using EzNutrition.Application.Consultations;
using EzNutrition.Archives.Contracts.Resources;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Domain.Assessments;

namespace EzNutrition.Application.Reports;

/// <summary>捕获已核算的能量、分配和交换份，供本机审核。</summary>
public sealed class EnergyReportFactory(ArchiveContractAssembler assembler)
{
    /// <summary>创建固定输入版本及展示配置。</summary>
    public EnergyReportDraft Create(ConsultationWorkspace workspace, CanonicalReference template,
        ActorReference? signer, DateTimeOffset capturedAt, SignedReport? previous = null, EnergyReportOptions? options = null)
    {
        options ??= new();
        options.Validate();
        RequireCalculated(workspace);
        if (string.IsNullOrWhiteSpace(template.Version)) throw new ArgumentException("报告模板必须声明版本。", nameof(template));
        if (signer is not null && (string.IsNullOrWhiteSpace(workspace.Client.Name)
            || signer.Identifier is null || string.IsNullOrWhiteSpace(signer.Display) || signer.AbsentReason is not null))
            throw new InvalidOperationException("正式报告需要患者姓名及完整的签发人身份。");
        return new EnergyReportDraft(ReportDraftAssembler.Create(assembler.CreateEnergyDocument(workspace, capturedAt),
            ArchiveResourceTypes.EnergyAssessment, "能量核算报告", template, signer, capturedAt, previous),
            signer, previous, workspace, options);
    }

    /// <summary>检查结果与当前输入相符，且分配值可用于报告。</summary>
    public static void RequireCalculated(ConsultationWorkspace workspace)
    {
        var calculator = workspace.CurrentEnergyCalculator;
        if (calculator is not { Energy: > 0, Allocation: not null, FoodExchangeAllocation: not null })
            throw new InvalidOperationException("请先完成总能量计算或核定，再生成报告。");
        if (!calculator.HasCurrentInputs)
            throw new InvalidOperationException("患者资料或 PAL 已变化，请重新核算后生成报告。");
        var allocation = calculator.Allocation;
        if (allocation.TotalEnergy != calculator.Energy || new[] { allocation.ProteinPercentage,
            allocation.FatPercentage, allocation.CarbohydratePercentage }.Any(value => !double.IsFinite(value) || value < 0 || value > 1))
            throw new InvalidOperationException("宏量营养素供能比例无效，请检查分配设置。");
        var exchanges = calculator.FoodExchangeAllocation;
        if (new[] { exchanges.GrainsAndStarchyFoods, exchanges.Fruits, exchanges.Vegetables,
            exchanges.MeatsAndEggs, exchanges.LegumesAndDairyAlternatives, exchanges.EnergyFoodsOrFats }
            .Any(value => !double.IsFinite(value) || value < 0))
            throw new InvalidOperationException("食物类别交换份存在负数或无效值，请调整后生成报告。");
    }
}

/// <summary>保存一个餐次采用的宏量营养素能量交换份。</summary>
public sealed record EnergyMealExchanges(string Meal, double Protein, double Carbohydrate, double Fat);

/// <summary>保存能量契约快照、展示配置及审核期间的输入一致性检查。</summary>
public sealed class EnergyReportDraft : ReportDraft
{
    private readonly ConsultationWorkspace workspace;
    private readonly EnergyCalculator calculator;
    private readonly object state;

    internal EnergyReportDraft(ArchiveDocument document, ActorReference? signer, SignedReport? previous,
        ConsultationWorkspace workspace, EnergyReportOptions options) : base(document, signer, previous)
    {
        this.workspace = workspace;
        calculator = workspace.CurrentEnergyCalculator!;
        state = State(workspace);
        Options = options;
        var allocation = calculator.Allocation!;
        MealExchanges = Array.AsReadOnly(new[]
        {
            new EnergyMealExchanges("早餐", allocation.BreakfastProteinPortions, allocation.BreakfastCarbohydratePortions, allocation.BreakfastFatPortions),
            new EnergyMealExchanges("午餐", allocation.LunchProteinPortions, allocation.LunchCarbohydratePortions, allocation.LunchFatPortionst),
            new EnergyMealExchanges("晚餐", allocation.DinnerProteinPortions, allocation.DinnerCarbohydratePortions, allocation.DinnerFatPortions)
        });
    }

    /// <summary>本次审核选择的节段。</summary>
    public EnergyReportOptions Options { get; }

    /// <summary>沿用领域计算结果，保留其按半份取整的值。</summary>
    public IReadOnlyList<EnergyMealExchanges> MealExchanges { get; }

    /// <summary>签发前确认患者、核算及分配均未变化。</summary>
    public override void EnsureCurrent()
    {
        if (!ReferenceEquals(workspace.CurrentEnergyCalculator, calculator) || !state.Equals(State(workspace)))
            throw new InvalidOperationException("能量核算或患者资料已变化，请重新生成并审核报告。");
        EnergyReportFactory.RequireCalculated(workspace);
    }

    private static object State(ConsultationWorkspace workspace)
    {
        var client = workspace.Client;
        var energy = workspace.CurrentEnergyCalculator!;
        var allocation = energy.Allocation;
        var exchanges = energy.FoodExchangeAllocation;
        return ((client.Name, client.Gender, client.Age, client.BirthDate, client.Height, client.Weight, client.SpecialPhysiologicalPeriod),
            (energy.PAL, energy.BMI, energy.Energy, energy.CalculatedEnergy, energy.CalculationMethod,
                energy.AppliedOffsetEnergy, energy.IsEnergyManuallyAdjusted, energy.SelectedEer?.BEE),
            (allocation, allocation?.ProteinPercentage, allocation?.FatPercentage),
            (exchanges, exchanges?.Fruits, exchanges?.Vegetables, exchanges?.LegumesAndDairyAlternatives, exchanges?.EnergyFoodsOrFats));
    }
}

/// <summary>由 Blazor 宿主共用的能量报告 PDF 渲染端口。</summary>
public interface IEnergyReportRenderer
{
    /// <summary>模板身份和确切版本。</summary>
    CanonicalReference Template { get; }
    /// <summary>渲染固定快照。</summary>
    ValueTask<byte[]> RenderAsync(EnergyReportDraft draft, CancellationToken cancellationToken = default);
}
