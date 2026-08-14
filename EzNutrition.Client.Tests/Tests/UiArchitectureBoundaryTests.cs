using EzNutrition.Application.Ports;
using EzNutrition.Client.Infrastructure;
using EzNutrition.UI.Components;
using EzNutrition.UI.Services;
using System.Reflection;

namespace EzNutrition.Client.Tests.Tests;

/// <summary>
/// Verifies that reusable UI components retain a one-way dependency on host-neutral layers.
/// </summary>
public sealed class UiArchitectureBoundaryTests
{
    private static readonly HashSet<string> ForbiddenUiAssemblyReferences =
    [
        "EzNutrition.Client",
        "EzNutrition.Server",
        "Microsoft.AspNetCore.Components.WebAssembly",
        "Microsoft.Extensions.Http",
        "System.Net.Http"
    ];

    private static readonly HashSet<string> ForbiddenUiPublicApiTypes =
    [
        "System.Net.Http.HttpClient",
        "System.Net.Http.IHttpClientFactory",
        "System.Net.Http.HttpRequestMessage",
        "System.Net.Http.HttpResponseMessage"
    ];

    /// <summary>
    /// Verifies that the consultation components and safe Markdown renderer are supplied by the UI library.
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
        Assert.Equal(uiAssembly, typeof(ArchiveActions).Assembly);
        Assert.Equal(uiAssembly, typeof(ArchiveCenter).Assembly);
        Assert.Equal(uiAssembly, typeof(ArchiveDocumentReview).Assembly);
        Assert.Equal(uiAssembly, typeof(LocalDateTimeDisplay).Assembly);
        Assert.Equal(uiAssembly, typeof(ILocalDateTimeFormatter).Assembly);
        Assert.Equal(uiAssembly, typeof(SafeMarkdown).Assembly);
    }

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
    /// Verifies that consumers cannot acquire HTTP transport types through the public UI surface.
    /// </summary>
    [Fact]
    public void Ui_public_api_does_not_expose_http_transport_types()
    {
        var violations = new List<string>();

        foreach (var exportedType in typeof(Advice).Assembly.GetExportedTypes())
        {
            AddViolationIfForbidden(violations, exportedType, exportedType.BaseType, "base type");
            foreach (var implementedInterface in exportedType.GetInterfaces())
            {
                AddViolationIfForbidden(violations, exportedType, implementedInterface, "implemented interface");
            }

            foreach (var member in exportedType.GetMembers(
                         BindingFlags.Public |
                         BindingFlags.Instance |
                         BindingFlags.Static |
                         BindingFlags.DeclaredOnly))
            {
                foreach (var signatureType in GetSignatureTypes(member))
                {
                    AddViolationIfForbidden(violations, member, signatureType, "signature");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "EzNutrition.UI public API exposes forbidden HTTP transport types:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    /// Verifies that the AI port belongs to Application and its browser adapter belongs to Client.
    /// </summary>
    [Fact]
    public void Ai_advice_gateway_has_the_expected_port_and_adapter_ownership()
    {
        Assert.Equal("EzNutrition.Application", typeof(IAiAdviceGateway).Assembly.GetName().Name);
        Assert.Equal("EzNutrition.Application.Ports", typeof(IAiAdviceGateway).Namespace);
        Assert.Equal("EzNutrition.Client", typeof(HttpAiAdviceGateway).Assembly.GetName().Name);
        Assert.Equal("EzNutrition.Client.Infrastructure", typeof(HttpAiAdviceGateway).Namespace);
        Assert.True(typeof(IAiAdviceGateway).IsAssignableFrom(typeof(HttpAiAdviceGateway)));
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

    private static IEnumerable<Type> GetSignatureTypes(MemberInfo member)
    {
        switch (member)
        {
            case MethodInfo method:
                yield return method.ReturnType;
                foreach (var parameter in method.GetParameters())
                {
                    yield return parameter.ParameterType;
                }

                foreach (var genericArgument in method.GetGenericArguments())
                {
                    foreach (var constraint in genericArgument.GetGenericParameterConstraints())
                    {
                        yield return constraint;
                    }
                }

                break;
            case ConstructorInfo constructor:
                foreach (var parameter in constructor.GetParameters())
                {
                    yield return parameter.ParameterType;
                }

                break;
            case PropertyInfo property:
                yield return property.PropertyType;
                foreach (var parameter in property.GetIndexParameters())
                {
                    yield return parameter.ParameterType;
                }

                break;
            case FieldInfo field:
                yield return field.FieldType;
                break;
            case EventInfo eventInfo when eventInfo.EventHandlerType is not null:
                yield return eventInfo.EventHandlerType;
                break;
        }
    }

    private static void AddViolationIfForbidden(
        ICollection<string> violations,
        MemberInfo owner,
        Type? candidate,
        string relationship)
    {
        var forbiddenType = FindForbiddenTransportType(candidate);
        if (forbiddenType is not null)
        {
            violations.Add($"{owner.DeclaringType?.FullName ?? owner.Name}.{owner.Name} ({relationship}) -> {forbiddenType}");
        }
    }

    private static Type? FindForbiddenTransportType(Type? candidate)
    {
        if (candidate is null)
        {
            return null;
        }

        if (candidate.FullName is { } fullName && ForbiddenUiPublicApiTypes.Contains(fullName))
        {
            return candidate;
        }

        if (candidate.HasElementType)
        {
            return FindForbiddenTransportType(candidate.GetElementType());
        }

        if (candidate.IsGenericType)
        {
            foreach (var genericArgument in candidate.GetGenericArguments())
            {
                var forbiddenType = FindForbiddenTransportType(genericArgument);
                if (forbiddenType is not null)
                {
                    return forbiddenType;
                }
            }
        }

        if (candidate.IsGenericParameter)
        {
            foreach (var constraint in candidate.GetGenericParameterConstraints())
            {
                var forbiddenType = FindForbiddenTransportType(constraint);
                if (forbiddenType is not null)
                {
                    return forbiddenType;
                }
            }
        }

        return null;
    }
}
