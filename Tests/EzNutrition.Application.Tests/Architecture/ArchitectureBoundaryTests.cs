using EzNutrition.Application.Consultations;
using EzNutrition.Domain.Assessments;

namespace EzNutrition.Application.Tests.Architecture;

/// <summary>
/// 验证可复用核心项目不会反向依赖前端或传输实现。
/// </summary>
public sealed class ArchitectureBoundaryTests
{
    /// <summary>
    /// 验证领域程序集不直接引用 HTTP、Blazor、AntDesign、EF 或应用层。
    /// </summary>
    [Fact]
    public void Domain_has_no_transport_ui_or_application_dependencies()
    {
        var references = typeof(EnergyCalculator).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("System.Net.Http", references);
        Assert.DoesNotContain("Microsoft.AspNetCore.Components", references);
        Assert.DoesNotContain("AntDesign", references);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", references);
        Assert.DoesNotContain("EzNutrition.Application", references);
        Assert.DoesNotContain("EzNutrition.Client", references);
        Assert.DoesNotContain("EzNutrition.Archives.Xml", references);
    }

    /// <summary>
    /// 验证应用程序集不直接引用 HTTP、Blazor、AntDesign、EF 或 WASM 前端。
    /// </summary>
    [Fact]
    public void Application_has_no_transport_ui_or_client_dependencies()
    {
        var references = typeof(ConsultationApplicationService).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("System.Net.Http", references);
        Assert.DoesNotContain("Microsoft.AspNetCore.Components", references);
        Assert.DoesNotContain("AntDesign", references);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", references);
        Assert.DoesNotContain("EzNutrition.Client", references);
        Assert.DoesNotContain("EzNutrition.Assessments.Common", references);
    }
}
