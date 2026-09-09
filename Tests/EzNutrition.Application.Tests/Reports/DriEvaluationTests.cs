using System.Text;
using EzNutrition.Application.Reports;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;
using EzNutrition.Shared.Data.Entities;

namespace EzNutrition.Application.Tests.Reports;

/// <summary>验证 DRIs 输出冻结已有结果，并保留参考类型、缺失与权限语义。</summary>
public sealed class DriEvaluationTests
{
    /// <summary>来源对象变化不改变结果快照；调整后的总量和 AMDR 上下限分别保留。</summary>
    [Fact]
    public void Capture_freezes_resolved_values_criteria_and_separate_bounds()
    {
        var client = new ClientInfo { Gender = "女", Age = new(35), SpecialPhysiologicalPeriod = "孕中期" };
        var records = new List<DietaryReferenceIntakeValue>
        {
            Value("蛋白质", DietaryReferenceIntakeType.RNI, 65),
            Value("蛋白质", DietaryReferenceIntakeType.RNI, 15, offset: true),
            Value("脂肪", DietaryReferenceIntakeType.AMDR_L, 20, "%"),
            Value("脂肪", DietaryReferenceIntakeType.AMDR_H, 30, "%"),
            Value("钠", DietaryReferenceIntakeType.AI, 0, "mg")
        };
        var result = new DRIs(client) { AvailableDRIs = records };
        var snapshot = DriEvaluationSnapshot.Capture(result, DateTimeOffset.UtcNow);
        records[0].Value = 1000;
        records[2].MeasureUnit = "被修改";
        client.Age = new(70);
        client.SpecialPhysiologicalPeriod = "";
        result.AvailableDRIs = [];

        Assert.Equal(35, snapshot.Age.Years);
        Assert.Equal("孕中期", snapshot.SpecialPeriod);
        Assert.Equal(80, snapshot.Values.Single(value => value.Type == DietaryReferenceIntakeType.RNI).Value);
        Assert.Equal(0, snapshot.Values.Single(value => value.Type == DietaryReferenceIntakeType.AI).Value);
        Assert.Equal(new decimal?[] { 20, 30 }, snapshot.Values.Where(value => value.Nutrient == "脂肪").Select(value => value.Value));
        Assert.All(snapshot.Values.Where(value => value.Nutrient == "脂肪"), value => Assert.Equal("%", value.Unit));
    }

    /// <summary>无法核定的参考值不变成零，整个营养素被排除时仍保留问题说明。</summary>
    [Fact]
    public void Capture_preserves_conflicts_and_rejects_empty_results()
    {
        var result = new DRIs(new ClientInfo { Gender = "女", Age = new(35) })
        {
            AvailableDRIs = [Value("蛋白质", DietaryReferenceIntakeType.RNI, 65), Value("蛋白质", DietaryReferenceIntakeType.RNI, 70),
                Value("钙", DietaryReferenceIntakeType.AI, 800, "mg"), Value("钙", DietaryReferenceIntakeType.AI, 1, "g")]
        };
        var snapshot = DriEvaluationSnapshot.Capture(result, DateTimeOffset.UtcNow);
        Assert.Null(Assert.Single(snapshot.Values).Value);
        Assert.Contains(snapshot.Issues, issue => issue.Nutrient == "蛋白质");
        Assert.Contains(snapshot.Issues, issue => issue.Nutrient == "钙");
        result.AvailableDRIs = [];
        Assert.Throws<InvalidOperationException>(() => DriEvaluationSnapshot.Capture(result, DateTimeOffset.UtcNow));
    }

    /// <summary>签发权限不参与速查输出；打印前失权或成品损坏时均不打开宿主窗口。</summary>
    [Fact]
    public async Task Workflow_rechecks_print_permission_and_rejects_invalid_pdf()
    {
        var result = new DRIs(new ClientInfo { Gender = "女", Age = new(35) })
        { AvailableDRIs = [Value("蛋白质", DietaryReferenceIntakeType.RNI, 65)] };
        var snapshot = DriEvaluationSnapshot.Capture(result, DateTimeOffset.UtcNow);
        var access = new Access();
        var renderer = new Renderer();
        var printer = new Printer();
        var workflow = new DriEvaluationWorkflow(access, renderer, printer);
        await workflow.PrintAsync(snapshot);
        Assert.Equal(1, printer.Calls);
        renderer.AfterRender = () => access.Allowed = false;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => workflow.PrintAsync(snapshot).AsTask());
        Assert.Equal(1, printer.Calls);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => workflow.PrintAsync(snapshot).AsTask());
        Assert.Equal(2, renderer.Calls);
        access.Allowed = true;
        renderer.AfterRender = null;
        renderer.Pdf = "invalid";
        await Assert.ThrowsAsync<InvalidDataException>(() => workflow.PrintAsync(snapshot).AsTask());
        Assert.Equal(1, printer.Calls);
    }

    private static DietaryReferenceIntakeValue Value(string nutrient, DietaryReferenceIntakeType type, decimal value,
        string unit = "g", bool offset = false) => new()
        { Nutrient = nutrient, RecordType = type, Value = value, MeasureUnit = unit, IsOffset = offset };

    private sealed class Access : IReportAuthorization
    {
        public bool Allowed { get; set; } = true;
        public ValueTask RequirePrintAsync(CancellationToken cancellationToken = default) =>
            Allowed ? ValueTask.CompletedTask : throw new UnauthorizedAccessException();
        public ValueTask<ActorReference> RequireIssuerAsync(CancellationToken cancellationToken = default) => throw new UnauthorizedAccessException();
    }
    private sealed class Renderer : IDriEvaluationRenderer
    {
        public int Calls { get; private set; }
        public Action? AfterRender { get; set; }
        public string Pdf { get; set; } = "%PDF-1.7\nevaluation";
        public ValueTask<byte[]> RenderAsync(DriEvaluationSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            Calls++;
            AfterRender?.Invoke();
            return ValueTask.FromResult(Encoding.UTF8.GetBytes(Pdf));
        }
    }
    private sealed class Printer : IReportPrinter
    {
        public int Calls { get; private set; }
        public ValueTask PrintAsync(ReadOnlyMemory<byte> pdf, string title, CancellationToken cancellationToken = default)
        { Calls++; return ValueTask.CompletedTask; }
    }
}
