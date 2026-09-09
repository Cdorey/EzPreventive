// 模板版本 3：共用量表正文，独立速查不显示正式报告编号；只消费已捕获的文字。
import { evaluationNotice, evaluationWatermark, table, renderPdf } from "./report-pdf.mjs";

/** 按 A4 排版，跨页表格重复表头，评估用途进入每一页的水印及页脚。 */
export function createDefinition(data) {
    const content = [
        { text: data.institution || "营养筛查与评估", fontSize: 11, color: "#47636b", margin: [0, 0, 0, 8] },
        { text: data.title, fontSize: 20, color: "#183b45", margin: [0, 0, 0, 8] },
        { text: `量表版本：${data.instrumentVersion}`, fontSize: 9, color: "#56636a", margin: [0, 0, 0, 12] },
        {
            table: { widths: ["*", "*"], body: [
                [`姓名：${data.patientName}`, `性别：${data.sex}    年龄：${data.age}`],
                [`身高：${data.height}`, `体重：${data.weight}`],
                [`评估时间：${data.assessedAt}`, `评估人员：${data.performer}`]
            ] },
            layout: "noBorders", margin: [0, 0, 0, 8]
        },
        { text: "评估结果", style: "section" },
        { text: `总分：${data.totalScore}`, fontSize: 13, margin: [0, 0, 0, 5] },
        { text: data.interpretation, margin: [0, 0, 0, 8] },
        { text: "评分明细", style: "section" },
        { text: "按本次作答路径列出适用项目；未回答项目保留标记。", fontSize: 9, color: "#56636a", margin: [0, 0, 0, 8] },
        table(["项目", "回答", "分值"], data.responses, ["32%", "*", 32])
    ];
    if (data.results.length) {
        content.push({ text: "分项结果", style: "section" }, table(["指标", "结果"], data.results, ["*", "30%"]));
    }
    return {
        pageSize: "A4",
        pageMargins: [40, 38, 40, 76],
        defaultStyle: { font: "ReportSans", fontSize: 10, lineHeight: 1.1, color: "#202d33" },
        styles: { section: { fontSize: 12, color: "#183b45", margin: [0, 10, 0, 6] } },
        info: { title: data.title, creator: "EzNutrition", producer: "EzNutrition local report renderer" },
        watermark: data.isEvaluation
            ? evaluationWatermark()
            : undefined,
        footer: (currentPage, pageCount) => ({
            margin: [40, 10, 40, 0], fontSize: 8, color: "#56636a",
            stack: [
                { text: data.isEvaluation ? evaluationNotice : `审核签发：${data.signer}`, margin: [0, 0, 0, 3] },
                { text: `${data.isEvaluation ? "生成" : "签发"}时间：${data.reportTime}`, margin: [0, 0, 0, 3] },
                { columns: [{ text: data.reportNumber
                    ? `报告编号：${data.reportNumber} · 第 ${data.revisionNumber} 版`
                    : "独立量表速查 · 未关联咨询档案", width: "*" },
                    { text: `${currentPage} / ${pageCount}`, alignment: "right", width: 45 }] }
            ]
        }),
        content
    };
}

/** 返回实际 PDF 字节，供 Blazor 保存原件；不打开打印窗口，不调用远程转换服务。 */
export async function render(data) {
    return await renderPdf(createDefinition(data));
}

/** 预览同一份待保存字节；隐藏查看器工具栏，应用自身的输出入口单独检查权限。 */
export function createPreview(bytes) {
    return URL.createObjectURL(new Blob([bytes], { type: "application/pdf" }));
}

/** 预览关闭后释放本机原件引用。 */
export function releasePreview(url) {
    URL.revokeObjectURL(url);
}
