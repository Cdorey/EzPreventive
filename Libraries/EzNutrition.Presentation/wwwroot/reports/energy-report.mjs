// 能量核算模板版本 1；表格消费审核时固定的领域计算结果。
import { evaluationNotice, evaluationWatermark, table, renderPdf } from "./report-pdf.mjs";

export function createDefinition(data) {
    const content = [
        { text: data.institution || "营养评估与分配", fontSize: 11, color: "#47636b", margin: [0, 0, 0, 8] },
        { text: data.title, fontSize: 20, color: "#183b45", margin: [0, 0, 0, 10] },
        { text: `姓名：${data.patient}`, fontSize: 12, margin: [0, 0, 0, 5] },
        { text: data.subject, margin: [0, 0, 0, 8] },
        { text: `本方案采用总能量：${data.energyTarget}`, fontSize: 12, color: "#183b45", margin: [0, 0, 0, 4] }
    ];
    const section = (title, headers, rows, widths, note) => {
        content.push({ text: title, style: "section", headlineLevel: 1 });
        if (note) content.push({ text: note, fontSize: 9, color: "#56636a", margin: [0, 0, 0, 6] });
        const grid = table(headers, rows, widths);
        grid.table.keepWithHeaderRows = 1;
        content.push(grid);
    };
    if (data.options.includeTotalEnergy) {
        section("总能量核算", ["项目", "核算与采用结果"], data.totalEnergy, [132, "*"]);
    }
    if (data.options.includeAllocation) {
        section("宏量营养素分配", ["营养素", "供能比例", "每日目标量"], data.macronutrients, ["*", "*", "*"],
            "每日目标量沿用三餐取整后的合计值。");
        section("三餐分配", ["餐次", "蛋白质", "碳水化合物", "脂肪"], data.meals, ["*", "*", "*", "*"],
            "早餐、午餐、晚餐按 30%、40%、30% 分配；各餐营养素量沿用核算取整结果。");
    }
    if (data.options.includeExchanges) {
        section("三餐能量交换份", ["餐次", "蛋白质供能", "碳水化合物供能", "脂肪供能"], data.mealExchanges, ["*", "*", "*", "*"],
            "单位：份。按每份 90 kcal 换算，取整至半份；各列对应宏量营养素提供的能量。");
        section("食物类别每日交换份", ["食物类别", "每日交换份（份）"], data.foodExchanges, ["*", "*"],
            "采用本次核算及人工调整后的食物类别分配。");
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
