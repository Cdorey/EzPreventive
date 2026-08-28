using System.Net;
using AntDesign;
using EzNutrition.Domain.Consultations;
using EzNutrition.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace EzNutrition.Client.Tests.Components;

public sealed class SoapContributionReviewModalRenderTests
{
    [Fact]
    public async Task Subjective_objective_candidate_hides_absent_assessment_and_plan()
    {
        var html = await RenderAsync(
            new SoapContribution("主观候选", "客观候选"),
            includeSubjective: true,
            includeObjective: true);

        Assert.Contains("主观资料（S）", html, StringComparison.Ordinal);
        Assert.Contains("客观资料（O）", html, StringComparison.Ordinal);
        Assert.DoesNotContain("问题评估（A）", html, StringComparison.Ordinal);
        Assert.DoesNotContain("处理计划（P）", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Assessment_candidate_uses_the_shared_modal_without_empty_subjective_or_objective_fields()
    {
        var html = await RenderAsync(
            new SoapContribution(Assessment: "选中的问题评估"),
            includeSubjective: false,
            includeObjective: false);

        Assert.DoesNotContain("主观资料（S）", html, StringComparison.Ordinal);
        Assert.DoesNotContain("客观资料（O）", html, StringComparison.Ordinal);
        Assert.Contains("问题评估（A）", html, StringComparison.Ordinal);
        Assert.DoesNotContain("处理计划（P）", html, StringComparison.Ordinal);
        Assert.Contains("选中的问题评估", html, StringComparison.Ordinal);
    }

    private static async Task<string> RenderAsync(
        SoapContribution candidate,
        bool includeSubjective,
        bool includeObjective)
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddAntDesign()
            .AddSingleton<NavigationManager, TestNavigationManager>()
            .AddSingleton<IJSRuntime, NoOpJsRuntime>()
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<SoapContributionReviewModal>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(SoapContributionReviewModal.Visible)] = true,
                    [nameof(SoapContributionReviewModal.Candidate)] = candidate,
                    [nameof(SoapContributionReviewModal.IncludeSubjective)] = includeSubjective,
                    [nameof(SoapContributionReviewModal.IncludeObjective)] = includeObjective,
                    [nameof(SoapContributionReviewModal.VisibleChanged)] =
                        EventCallback.Factory.Create<bool>(new object(), _ => { }),
                    [nameof(SoapContributionReviewModal.OnConfirmed)] =
                        EventCallback.Factory.Create<SoapContribution>(new object(), _ => { })
                }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
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
