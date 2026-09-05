using EzNutrition.Client.Infrastructure;
using EzNutrition.Presentation.Infrastructure;
using EzNutrition.UI.Components;

namespace EzNutrition.Client.Tests.Tests;

/// <summary>
/// Verifies that reusable UI components retain a one-way dependency on host-neutral layers.
/// </summary>
public sealed class UiArchitectureBoundaryTests
{
    private static readonly HashSet<string> ForbiddenUiAssemblyReferences =
    [
        "EzNutrition.Client",
        "EzNutrition.Assessments.Common",
        "EzNutrition.Presentation",
        "EzNutrition.Server",
        "EzNutrition.Archives.Xml",
        "Microsoft.AspNetCore.Components.WebAssembly",
        "Microsoft.Extensions.Http",
        "System.Net.Http"
    ];

    private static readonly HashSet<string> ForbiddenPresentationAssemblyReferences =
    [
        "EzNutrition.Client",
        "EzNutrition.Assessments.Common",
        "EzNutrition.Wpf",
        "EzNutrition.Server",
        "Microsoft.AspNetCore.Components.WebAssembly",
        "Microsoft.AspNetCore.Components.WebAssembly.Authentication"
    ];

    /// <summary>
    /// Verifies that the UI library has no direct dependency on a concrete host or HTTP transport assembly.
    /// </summary>
    [Fact]
    public void Ui_library_has_no_host_or_http_dependencies()
    {
        var references = typeof(Advice).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .Cast<string>()
            .ToArray();

        Assert.DoesNotContain(references, ForbiddenUiAssemblyReferences.Contains);
    }

    /// <summary>
    /// 验证完整工作台由共享 Presentation 类库提供，并且该类库不依赖任何具体宿主。
    /// </summary>
    [Fact]
    public void Presentation_workbench_is_shared_without_host_dependencies()
    {
        var presentationAssembly = typeof(EzNutrition.Presentation.App).Assembly;

        Assert.Equal(
            presentationAssembly,
            typeof(EzNutrition.Presentation.Shared.MainLayout).Assembly);
        Assert.Equal(
            presentationAssembly,
            typeof(EzNutrition.Presentation.Pages.MainTreatment).Assembly);
        Assert.Equal(
            presentationAssembly,
            typeof(HttpAiAdviceGateway).Assembly);

        var references = presentationAssembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .Cast<string>()
            .ToArray();

        Assert.DoesNotContain(references, ForbiddenPresentationAssemblyReferences.Contains);
    }

    /// <summary>
    /// 验证 WASM 只复用共享 Presentation 类库，不引用桌面宿主程序集。
    /// </summary>
    [Fact]
    public void Wasm_host_references_presentation_but_not_wpf_host()
    {
        var references = typeof(BrowserArchiveGateway).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .Cast<string>()
            .ToArray();

        Assert.Contains("EzNutrition.Presentation", references);
        Assert.DoesNotContain("EzNutrition.Wpf", references);
    }

    /// <summary>
    /// Verifies that browser archive storage is a host adapter and XML remains outside the reusable UI.
    /// </summary>
    [Fact]
    public void Archive_adapters_have_the_expected_ownership()
    {
        Assert.Equal("EzNutrition.Client", typeof(BrowserArchiveGateway).Assembly.GetName().Name);
        Assert.True(typeof(EzNutrition.Application.Archives.IArchiveDocumentStore)
            .IsAssignableFrom(typeof(BrowserArchiveGateway)));
        Assert.True(typeof(EzNutrition.Application.Archives.IArchiveDocumentTransport)
            .IsAssignableFrom(typeof(BrowserArchiveGateway)));
        Assert.Equal("EzNutrition.Archives.Xml", typeof(EzNutrition.Archives.Xml.XmlArchiveCodec).Assembly.GetName().Name);
        Assert.DoesNotContain(
            "EzNutrition.Archives.Xml",
            typeof(ArchiveCenter).Assembly.GetReferencedAssemblies().Select(reference => reference.Name));
    }
}
