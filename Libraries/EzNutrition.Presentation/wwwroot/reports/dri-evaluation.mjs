// DRIs 速查模板版本 1：仅呈现已经捕获的结果，不产生正式报告或执行参考数据查询。
import { evaluationNotice, evaluationWatermark, table, renderPdf } from "./report-pdf.mjs";

/** 所有参考类型逐项列出，冲突和来源说明进入 PDF，不依赖页面浮层。 */
export function createDefinition(data) {
    const content = [
        { text: "DRIs 速查 · 评估稿", fontSize: 20, color: "#183b45", margin: [0, 0, 0, 12] },
        { text: `${data.gender} · ${data.age} · ${data.specialPeriod}`, margin: [0, 0, 0, 8] },
        { text: "膳食参考摄入量查询结果；未关联患者或咨询，不构成已签发的个体营养处方。", fontSize: 9, color: "#56636a", margin: [0, 0, 0, 12] }
    ];
    if (data.issues.length) content.push(
        { text: "部分参考值需要人工核定", color: "#8c4e00", margin: [0, 0, 0, 6] },
        ...data.issues.map(text => ({ text, fontSize: 9, margin: [0, 0, 0, 5] }))
    );
    if (data.rows.length) content.push(table(["营养素", "参考指标", "数值", "说明"], data.rows, ["20%", "18%", "25%", "*"]));
    content.push({ text: "EAR：平均需要量；RNI：推荐摄入量；AI：适宜摄入量；UL：可耐受最高摄入量；AMDR：可接受宏量营养素分布范围；PI-NCD：慢性病预防建议摄入量；SPL：特定建议值。上下限及调整量按各自标记解释。",
        fontSize: 8, color: "#56636a", margin: [0, 12, 0, 0] });
    return {
        pageSize: "A4", pageMargins: [40, 38, 40, 70],
        defaultStyle: { font: "ReportSans", fontSize: 10, lineHeight: 1.1, color: "#202d33" },
        info: { title: "DRIs 速查 · 评估稿", creator: "EzNutrition", producer: "EzNutrition local report renderer" },
        watermark: evaluationWatermark(),
        footer: (page, count) => ({ margin: [40, 10, 40, 0], fontSize: 8, color: "#56636a", stack: [
            { text: evaluationNotice, margin: [0, 0, 0, 3] },
            { text: `结果取得时间：${data.retrievedAt}`, margin: [0, 0, 0, 3] },
            { columns: ["独立 DRIs 速查 · 未关联咨询档案", { text: `${page} / ${count}`, alignment: "right", width: 45 }] }
        ] }),
        content
    };
}

/** 返回本机生成的 PDF 原始字节，实际打印由宿主承担。 */
export async function render(data) {
    return await renderPdf(createDefinition(data));
}
