using EzNutrition.Archives.Contracts.Abstractions;

namespace EzNutrition.Archives.Contracts.Tests.Tests;

/// <summary>
/// 验证档案契约类库保持对存储格式和应用运行时的隔离。
/// </summary>
public sealed class ArchitectureIsolationTests
{
    /// <summary>
    /// 验证 Contracts 程序集没有引用 XML、Blazor、ASP.NET Core 或 Entity Framework Core。
    /// </summary>
    [Fact]
    public void Contracts_assembly_has_no_format_ui_or_database_dependencies()
    {
        var referencedAssemblyNames = typeof(IArchiveResource).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();
        var forbiddenPrefixes = new[]
        {
            "System.Xml",
            "Microsoft.AspNetCore",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore.Components"
        };

        foreach (var forbiddenPrefix in forbiddenPrefixes)
        {
            Assert.DoesNotContain(
                referencedAssemblyNames,
                name => name.StartsWith(forbiddenPrefix, StringComparison.Ordinal));
        }
    }
}
