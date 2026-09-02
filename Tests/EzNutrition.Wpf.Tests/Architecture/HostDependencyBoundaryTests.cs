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
        Assert.Contains("Velopack", references);
        Assert.DoesNotContain("EzNutrition.Client", references);
    }

    /// <summary>
    /// 验证 DPAPI、证书策略和用户设置实现只存在于桌面宿主程序集。
    /// </summary>
    [Fact]
    public void Desktop_connection_security_adapters_are_owned_by_wpf_host()
    {
        var wpfAssembly = typeof(EzNutrition.Wpf.App).Assembly;

        Assert.Equal(
            wpfAssembly,
            typeof(EzNutrition.Wpf.Security.DpapiLoginCredentialStore).Assembly);
        Assert.Equal(
            wpfAssembly,
            typeof(EzNutrition.Wpf.Networking.WpfHttpMessageHandlerFactory).Assembly);
        Assert.Equal(
            wpfAssembly,
            typeof(EzNutrition.Wpf.Configuration.WpfUserSettingsStore).Assembly);

        var presentationReferences = typeof(EzNutrition.Presentation.App).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .Cast<string>()
            .ToArray();
        Assert.DoesNotContain("System.Security.Cryptography.ProtectedData", presentationReferences);
        Assert.DoesNotContain("Velopack", presentationReferences);
    }

    /// <summary>
    /// 验证共享层只定义辅助页面契约，原生多窗口实现仍由 WPF 宿主持有。
    /// </summary>
    [Fact]
    public void Auxiliary_window_implementation_is_owned_by_wpf_host()
    {
        var presentationAssembly = typeof(EzNutrition.Presentation.App).Assembly;
        var wpfAssembly = typeof(EzNutrition.Wpf.App).Assembly;

        Assert.Equal(
            presentationAssembly,
            typeof(EzNutrition.Presentation.Services.IAuxiliaryPageHost).Assembly);
        Assert.Equal(
            wpfAssembly,
            typeof(EzNutrition.Wpf.Desktop.WpfAuxiliaryPageHost).Assembly);
    }
}
