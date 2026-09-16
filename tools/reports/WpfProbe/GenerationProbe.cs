using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Threading;
using EzNutrition.Application.Archives;
using EzNutrition.Application.Consultations;
using EzNutrition.Application.Reports;
using EzNutrition.Archives.Contracts.ValueObjects;
using EzNutrition.Assessments.Common;
using EzNutrition.Domain.Assessments;
using EzNutrition.Domain.Consultations;
using EzNutrition.Presentation.Reports;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using ApplicationIdentity = EzNutrition.Archives.Contracts.ValueObjects.ApplicationIdentity;

/// <summary>在真实 WPF BlazorWebView 中运行产品 PDF 适配器；只使用合成输入和本机资源。</summary>
internal static class GenerationProbe
{
    /// <summary>创建隔离组件宿主并等待生成完成；图形初始化失败也作为明确的失败结果返回。</summary>
    public static async Task RunAsync(string output)
    {
        Directory.CreateDirectory(output);
        var state = new ProbeState(output);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWpfBlazorWebView();
        services.AddSingleton(state);
        services.AddScoped<IAssessmentReportRenderer, PdfMakeAssessmentReportRenderer>();
        services.AddScoped<IDriEvaluationRenderer, PdfMakeDriEvaluationRenderer>();
        await using var provider = services.BuildServiceProvider();
        var view = new BlazorWebView { HostPage = "wwwroot/hybrid.html", Services = provider };
        view.RootComponents.Add(new RootComponent { Selector = "#app", ComponentType = typeof(ProbeComponent) });
        view.BlazorWebViewInitializing += (_, args) => args.UserDataFolder = Path.Combine(output, "WebView2");
        CoreWebView2? core = null;
        view.BlazorWebViewInitialized += (_, args) =>
        {
            core = args.WebView.CoreWebView2;
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (_, request) =>
            {
                var uri = new Uri(request.Request.Uri);
                state.Requests.Add(uri.AbsoluteUri);
                if (uri.Host is not ("0.0.0.0" or "0.0.0.1"))
                {
                    request.Response = core.Environment.CreateWebResourceResponse(null, 403, "Forbidden", "");
                    state.Completion.TrySetException(new InvalidOperationException($"PDF 生成意外访问外部地址：{uri.Host}"));
                }
            };
        };
        // 使用透明且不接收输入的窗口，避免抢占用户工作台。
        var window = new Window { Content = view, Width = 1100, Height = 800, ShowActivated = false,
            ShowInTaskbar = false, IsHitTestVisible = false, Opacity = 0,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = 0, Top = 0 };
        DispatcherUnhandledExceptionEventHandler failure = (_, args) =>
        {
            args.Handled = true;
            state.Completion.TrySetException(args.Exception);
        };
        System.Windows.Application.Current.DispatcherUnhandledException += failure;
        try
        {
            window.Show();
            await state.Completion.Task.WaitAsync(TimeSpan.FromSeconds(60));
            if (!state.Requests.Any(uri => uri.EndsWith("NotoSansCJKsc-Regular.otf", StringComparison.Ordinal)))
                throw new InvalidOperationException("未观察到实际字体加载，不能确认本机 PDF 生成路径。");
            await File.WriteAllLinesAsync(Path.Combine(output, "requests.txt"), state.Requests);
            await using var screenshot = File.Create(Path.Combine(output, "generation.png"));
            await core!.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, screenshot);
            Console.WriteLine("七份 PDF 已生成；正在释放 BlazorWebView。");
        }
        catch (Exception error)
        {
            await File.WriteAllTextAsync(Path.Combine(output, "generation-failure.txt"), error.ToString());
            throw;
        }
        finally
        {
            try
            {
                // 将退出挂起作为验收失败，不让自动化无限等待，也不掩盖生成或清理错误。
                await view.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15));
            }
            finally
            {
                window.Close();
                System.Windows.Application.Current.DispatcherUnhandledException -= failure;
            }
        }
        Console.WriteLine("WPF Blazor 本机生成与控件释放完成；请继续检查成品内容和视觉效果。");
    }

    /// <summary>保存本次探针的输出位置和完成状态，仅在隔离宿主中使用。</summary>
    /// <param name="Output">合成成品与诊断文件的输出目录。</param>
    public sealed record ProbeState(string Output)
    {
        /// <summary>异步通知完成，避免在组件回调内部同步销毁其宿主。</summary>
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>记录真实资源请求以检查是否意外访问外部地址。</summary>
        public List<string> Requests { get; } = [];
    }

    /// <summary>通过 Blazor 组件作用域取得真实 JS 运行时并生成成品，不用模拟 JS 返回值。</summary>
    public sealed class ProbeComponent : ComponentBase
    {
        /// <summary>取得产品量表 PDF 适配器及其真实 JS 运行时。</summary>
        [Inject] public IAssessmentReportRenderer AssessmentRenderer { get; set; } = null!;
        /// <summary>取得产品 DRIs PDF 适配器。</summary>
        [Inject] public IDriEvaluationRenderer DriRenderer { get; set; } = null!;
        /// <summary>取得本次探针的隔离状态。</summary>
        [Inject] public ProbeState State { get; set; } = null!;

        protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, "正在验证本机报告生成…");

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender) return;
            try
            {
                var factory = new AssessmentReportFactory(new ArchiveContractAssembler(
                    new ApplicationIdentity(new Uri("urn:test:wpf-report"), "合成报告验收", "1")));
                var signer = new ActorReference { Identifier = new BusinessIdentifier(new Uri("urn:test:users"), "doctor"), Display = "模拟医师" };
                (INutritionAssessmentInstrument Instrument, string[] Answers)[] samples =
                [
                    (new MustInstrument(), ["above-20", "below-five-percent", "absent"]),
                    (new Nrs2002Instrument(), ["bmi-at-least-18-5", "no-scored-weight-loss", "no-scored-intake-reduction", "no-scored-disease-severity"]),
                    (new MnaSfInstrument(), ["unchanged", "none", "goes-out", "no", "none"])
                ];
                foreach (var (instrument, answers) in samples)
                {
                    var workspace = new ConsultationWorkspace(new ClientInfo
                    { Name = "模拟报告患者", Gender = "女", Age = new(70), Height = 165, Weight = 60 });
                    var service = new NutritionAssessmentApplicationService([instrument]);
                    var run = service.StartRun(workspace, instrument.Definition);
                    var items = run.Definition.Items.ToArray();
                    for (var i = 0; i < answers.Length; i++) run.SetAnswer(items[i].Code, answers[i]);
                    var draft = factory.Create(workspace, run, AssessmentRenderer.Template, signer, DateTimeOffset.UtcNow);
                    await SaveAsync($"{instrument.Definition.Code}-formal.pdf", await AssessmentRenderer.RenderAsync(draft));
                    await SaveAsync($"{instrument.Definition.Code}-evaluation.pdf",
                        await AssessmentRenderer.RenderEvaluationAsync(NutritionAssessmentSnapshot.Capture(run), DateTimeOffset.UtcNow));
                }
                var dris = new DRIs(new ClientInfo { Gender = "女", Age = new(35) })
                { AvailableDRIs = [new() { Nutrient = "蛋白质", RecordType = EzNutrition.Shared.Data.Entities.DietaryReferenceIntakeType.RNI, Value = 65, MeasureUnit = "g" }] };
                await SaveAsync("dris-evaluation.pdf", await DriRenderer.RenderAsync(DriEvaluationSnapshot.Capture(dris, DateTimeOffset.UtcNow)));
                State.Completion.TrySetResult();
            }
            catch (Exception error) { State.Completion.TrySetException(error); }
        }

        private async Task SaveAsync(string name, byte[] bytes)
        {
            _ = ReportPdf.Identity(bytes);
            await File.WriteAllBytesAsync(Path.Combine(State.Output, name), bytes);
            await File.AppendAllTextAsync(Path.Combine(State.Output, "generated-sha256.txt"), $"{name}: {Convert.ToHexString(SHA256.HashData(bytes))}\n");
        }
    }
}
