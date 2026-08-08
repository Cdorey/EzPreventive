using EzNutrition.UI.Components;
using EzNutrition.UI.Services;

namespace EzNutrition.Client.Tests.Tests;

/// <summary>
/// 验证可复用 UI 组件与具体宿主保持单向依赖。
/// </summary>
public sealed class UiArchitectureBoundaryTests
{
    /// <summary>
    /// 验证营养咨询组件和安全 Markdown 渲染器均由 UI 类库提供。
    /// </summary>
    [Fact]
    public void Consultation_components_are_provided_by_ui_library()
    {
        var uiAssembly = typeof(Advice).Assembly;

        Assert.Equal(uiAssembly, typeof(DietarySurvey).Assembly);
        Assert.Equal(uiAssembly, typeof(DRIsInSightTable).Assembly);
        Assert.Equal(uiAssembly, typeof(EnergyCalculatorTreatment).Assembly);
        Assert.Equal(uiAssembly, typeof(MedicalInformation).Assembly);
        Assert.Equal(uiAssembly, typeof(Summary).Assembly);
        Assert.Equal(uiAssembly, typeof(SafeMarkdown).Assembly);
    }

    /// <summary>
    /// 验证 UI 类库不直接依赖 WASM、服务端或浏览器专用宿主程序集。
    /// </summary>
    [Fact]
    public void Ui_library_has_no_wasm_or_server_dependencies()
    {
        var references = typeof(Advice).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("EzNutrition.Client", references);
        Assert.DoesNotContain("EzNutrition.Server", references);
        Assert.DoesNotContain("Microsoft.AspNetCore.Components.WebAssembly", references);
    }
}
