using System.Globalization;
using EzNutrition.Application.Reports;
using EzNutrition.Shared.Data.Entities;
using Microsoft.JSInterop;

namespace EzNutrition.Presentation.Reports;

/// <summary>将 DRIs 结果格式化后交给本机 PDF 模板，与量表共用字体和水印资源。</summary>
public sealed class PdfMakeDriEvaluationRenderer(IJSRuntime js) : IDriEvaluationRenderer
{
    /// <inheritdoc />
    public async ValueTask<byte[]> RenderAsync(DriEvaluationSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var model = new
        {
            Gender = snapshot.Gender,
            Age = snapshot.Age.ToString(),
            SpecialPeriod = string.IsNullOrWhiteSpace(snapshot.SpecialPeriod) ? "无特殊生理状况" : snapshot.SpecialPeriod,
            RetrievedAt = snapshot.RetrievedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            Rows = snapshot.Values.Select(value => new[]
            {
                value.Nutrient,
                value.Type switch
                {
                    DietaryReferenceIntakeType.AMDR_L => "AMDR 下限",
                    DietaryReferenceIntakeType.AMDR_H => "AMDR 上限",
                    _ => value.Type.ToString().Replace('_', '-')
                },
                value.Value is { } number ? $"{number.ToString(CultureInfo.InvariantCulture)} {value.Unit}"
                    : "数据存在冲突，需手工核定",
                value.Note
            }).ToArray(),
            Issues = snapshot.Issues.Select(issue => $"{issue.Nutrient}：{issue.Message}").ToArray()
        };
        // 句柄随本次操作释放；宿主关闭时不再通过已停止接收消息的 WebView 清理模块。
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", cancellationToken,
            "./_content/EzNutrition.Presentation/reports/dri-evaluation.mjs");
        return await module.InvokeAsync<byte[]>("render", cancellationToken, model);
    }
}
