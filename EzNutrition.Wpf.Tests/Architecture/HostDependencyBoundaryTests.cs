namespace EzNutrition.Wpf.Tests.Architecture;

/// <summary>
/// 保护桌面宿主与其他可执行宿主之间的依赖边界。
/// </summary>
public sealed class HostDependencyBoundaryTests
{
    /// <summary>
    /// 验证 WPF 只复用共享 Presentation 类库，不引用 WASM 宿主程序集。
    /// </summary>
    [Fact]
    public void Wpf_host_references_presentation_but_not_wasm_host()
    {
        var references = typeof(EzNutrition.Wpf.App).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .Cast<string>()
            .ToArray();

        Assert.Contains("EzNutrition.Presentation", references);
        Assert.DoesNotContain("EzNutrition.Client", references);
    }
}
