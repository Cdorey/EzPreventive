// SOAP 模板版本 1；四节内容按纯文本渲染并保留换行。
import { evaluationNotice, evaluationWatermark, renderPdf } from "./report-pdf.mjs";

export function createDefinition(data) {
    const content = [
        { text: data.institution || "营养咨询记录", fontSize: 11, color: "#47636b", margin: [0, 0, 0, 8] },
        { text: data.title, fontSize: 20, color: "#183b45", margin: [0, 0, 0, 10] },
        { text: `姓名：${data.patient}`, fontSize: 12, margin: [0, 0, 0, 5] },
        { text: data.subject, margin: [0, 0, 0, 8] }
    ];
    for (const section of data.sections) {
        content.push({ text: section.title, style: "section", headlineLevel: 1 });
        content.push({ text: section.text || " ", margin: [0, 0, 0, 5], lineHeight: 1.25 });
    }
    return {
        pageSize: "A4", pageMargins: [40, 40, 40, 82],
        defaultStyle: { font: "ReportSans", fontSize: 10, lineHeight: 1, color: "#202d33" },
        styles: { section: { fontSize: 12, color: "#183b45", margin: [0, 13, 0, 7] } },
        pageBreakBefore: node => node.headlineLevel === 1 && node.startPosition.top > 625,
        info: { title: data.title, creator: "EzNutrition", producer: "EzNutrition local report renderer" },
        watermark: data.isEvaluation ? evaluationWatermark() : undefined,
        footer: (page, count) => ({ margin: [40, 10, 40, 0], fontSize: 8, color: "#56636a", stack: [
            { text: data.isEvaluation ? evaluationNotice : `审核签发：${data.signer}`, margin: [0, 0, 0, 3] },
            { text: `${data.isEvaluation ? "生成" : "签发"}时间：${data.reportTime}`, margin: [0, 0, 0, 3] },
            { columns: [{ text: `报告编号：${data.reportNumber} · 第 ${data.revisionNumber} 版`, width: "*" },
                { text: `${page} / ${count}`, alignment: "right", width: 45 }] }
        ] }), content
    };
}

export async function render(data) { return renderPdf(createDefinition(data)); }
