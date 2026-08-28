using System.Net;
using AntDesign;
using EzNutrition.Application.Archives;
using EzNutrition.Application.Consultations;
using EzNutrition.Application.Ports;
using EzNutrition.Assessments.Common;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;
using EzNutrition.Presentation;
using EzNutrition.Presentation.Pages;
using EzNutrition.Presentation.Services;
using EzNutrition.Shared.Data.DTO.PromptDto;
using EzNutrition.Shared.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using ArchiveApplicationIdentity =
    EzNutrition.Archives.Contracts.ValueObjects.ApplicationIdentity;

namespace EzNutrition.Client.Tests.Components;

/// <summary>
/// 验证营养咨询工作台在真实组件组合下能够完成服务端渲染。
/// </summary>
public sealed class MainTreatmentRenderTests
{
    /// <summary>
    /// 验证进入核算阶段后，量表目录按钮使用 Ant Design Button 支持的可访问名称参数。
    /// </summary>
    [Fact]
    public async Task Active_workspace_renders_the_assessment_catalog_action()
    {
        await using var services = BuildServiceProvider();
        var workspaceManager = services.GetRequiredService<ConsultationWorkspaceManager>();
        var client = new ClientInfo
        {
            Gender = "女",
            Age = new ChronologicalAge(70),
            Height = 165m,
            Weight = 60m
        };
        var workspace = new ConsultationWorkspace(client)
        {
            ClientInfoFormEnabled = false,
            SubjectiveObjectiveAssessmentPlanInformation = new()
        };
        workspaceManager[client.ClientId] = workspace;

        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<MainTreatment>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(MainTreatment.Id)] = client.ClientId.ToString()
                }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });

        Assert.Contains("assessment-add-button", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"添加量表\"", html, StringComparison.Ordinal);
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEzNutritionPresentation(
            new Uri("https://app.example.test/"),
            TimeZoneInfo.Utc);
        services.AddCascadingAuthenticationState();
        services.AddSingleton<NavigationManager, TestNavigationManager>();
        services.AddSingleton<IJSRuntime, NoOpJsRuntime>();
        services.AddSingleton<INutritionAssessmentInstrument, Nrs2002Instrument>();
        var nutritionDataSource = new EmptyNutritionDataSource();
        services.AddSingleton<IEnergyReferenceDataSource>(nutritionDataSource);
        services.AddSingleton<IDietaryReferenceIntakeDataSource>(nutritionDataSource);
        services.AddSingleton<IFoodCompositionDataSource>(nutritionDataSource);
        services.AddSingleton<IAiAdviceGateway, EmptyAiAdviceGateway>();
        services.AddSingleton<IArchiveWorkflow, UnavailableArchiveWorkflow>();
        services.AddSingleton(new ArchiveContractAssembler(new ArchiveApplicationIdentity(
            new Uri("https://app.example.test/assessment-render-test"),
            "工作台渲染测试",
            "2.1-test")));
        return services.BuildServiceProvider();
    }

    private sealed class EmptyNutritionDataSource :
        IEnergyReferenceDataSource,
        IDietaryReferenceIntakeDataSource,
        IFoodCompositionDataSource
    {
        public Task<IReadOnlyList<EER>> GetEnergyReferencesAsync(
            NutritionSubjectQuery subject,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EER>>([]);

        public Task<IReadOnlyList<DietaryReferenceIntakeValue>> GetDietaryReferenceIntakesAsync(
            NutritionSubjectQuery subject,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DietaryReferenceIntakeValue>>([]);

        public Task<IReadOnlyList<Food>> GetFoodsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Food>>([]);

        public Task<IReadOnlyList<Nutrient>> GetNutrientsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Nutrient>>([]);

        public Task<IReadOnlyList<FoodNutrientValue>> GetFoodCompositionAsync(
            string friendlyCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FoodNutrientValue>>([]);
    }

    private sealed class EmptyAiAdviceGateway : IAiAdviceGateway
    {
        public Task<EnvironmentDto?> GetEnvironmentAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EnvironmentDto?>(null);

        public async IAsyncEnumerable<AiAdviceGatewayUpdate> GenerateAsync(
            AiAdviceRequestDto request,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class UnavailableArchiveWorkflow : IArchiveWorkflow
    {
        public ArchiveWorkflowCapabilities Capabilities => ArchiveWorkflowCapabilities.None;

        public ValueTask<ArchiveOperationResult> SaveCurrentAsync(
            ConsultationWorkspace workspace,
            CancellationToken cancellationToken = default) => throw Unavailable();

        public ValueTask<ArchiveBrowseResult> BrowseAsync(
            CancellationToken cancellationToken = default) => throw Unavailable();

        public ValueTask<ArchiveOpenResult> OpenStoredAsync(
            Guid documentId,
            CancellationToken cancellationToken = default) => throw Unavailable();

        public ValueTask<ArchiveOperationResult> ExportStoredAsync(
            Guid documentId,
            CancellationToken cancellationToken = default) => throw Unavailable();

        public ValueTask<ArchiveOperationResult> DeleteStoredAsync(
            Guid documentId,
            CancellationToken cancellationToken = default) => throw Unavailable();

        public ValueTask<ArchiveOperationResult> ClearStoredAsync(
            CancellationToken cancellationToken = default) => throw Unavailable();

        public ValueTask<ArchiveOpenResult> ImportAsync(
            CancellationToken cancellationToken = default) => throw Unavailable();

        public ValueTask<ArchiveOperationResult> ExportCurrentAsync(
            ConsultationWorkspace workspace,
            CancellationToken cancellationToken = default) => throw Unavailable();

        private static NotSupportedException Unavailable() =>
            new("当前渲染测试不提供档案操作。");
    }

    private sealed class NoOpJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager()
        {
            Initialize("https://app.example.test/", "https://app.example.test/");
        }

        protected override void NavigateToCore(string uri, bool forceLoad) =>
            Uri = ToAbsoluteUri(uri).AbsoluteUri;

        protected override void NavigateToCore(string uri, NavigationOptions options) =>
            Uri = ToAbsoluteUri(uri).AbsoluteUri;
    }
}
