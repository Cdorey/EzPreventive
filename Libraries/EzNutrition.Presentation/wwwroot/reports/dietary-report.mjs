// 膳食调查模板版本 4；正文只消费固定快照中的文字。
import { evaluationNotice, evaluationWatermark, table, renderPdf } from "./report-pdf.mjs";

export function createDefinition(data) {
    const content = [
        { text: data.institution || "膳食摄入评估", fontSize: 11, color: "#47636b", margin: [0, 0, 0, 8] },
        { text: data.title, fontSize: 20, color: "#183b45", margin: [0, 0, 0, 10] },
        { text: `姓名：${data.patient}`, fontSize: 12, margin: [0, 0, 0, 5] },
        { text: data.subject, margin: [0, 0, 0, 5] },
        { text: `调查方法：24 小时膳食回顾法 · ${data.recallPeriod}`, color: "#56636a", margin: [0, 0, 0, 10] },
        { text: "本结果反映本次回顾日摄入，与参考值的比较用于进一步问询；单日回顾不代表通常摄入水平，也不单独构成营养诊断。", color: "#56636a", fontSize: 9, margin: [0, 0, 0, 4] }
    ];
    const section = (title, headers, rows, widths, note) => {
        if (!rows.length) return;
        content.push({ text: title, style: "section", headlineLevel: 1 });
        if (note) content.push({ text: note, fontSize: 9, color: "#56636a", margin: [0, 0, 0, 6] });
        const grid = table(headers, rows, widths);
        grid.table.keepWithHeaderRows = 1;
        grid.layout.paddingTop = () => 2;
        grid.layout.paddingBottom = () => 2;
        content.push(grid);
    };
    section("食物与餐次记录", ["餐次", "食物", "记录重量", "采用可食比例", "核算可食重量"], data.foods,
        [48, "*", 64, 70, 74], "采用可食比例为 100% 时，核算使用全部记录重量。");
    section("餐次能量分布", ["餐次", "能量", "占记录总能量"], data.meals, ["*", "*", "*"], "百分比沿用本次核算的取整结果。");
    content.push({ text: "营养素与参考比较", style: "section", headlineLevel: 1 });
    content.push({ text: "↑ 高于参考上界；↓ 低于参考下界。范围内不标箭头；— 表示未采用参考界值。单侧参考用 ≥ 或 ≤ 表示，类型中的空端表示该边界未提供。"
            + (data.includeDriReferences ? "专项参考见 DRIs 参考资料。" : ""),
        fontSize: 8, color: "#56636a", margin: [0, 0, 0, 4] });
    for (const group of data.nutrientGroups) {
        section(group.title, ["项目", "核算值", "参考范围", "参考类型"], group.rows.map(row => [
            { text: row.name, margin: [row.indented ? 12 : 0, 0, 0, 0] },
            { text: [{ text: row.amount }, { text: row.marker ? `  ${row.marker}` : "", color: "#9b3b32", fontSize: 11 }] },
            row.referenceRange,
            { text: row.referenceTypes, fontSize: 8 }
        ]), [115, 90, 120, "*"]);
    }
    content.push({ text: "三大宏量营养素的食材贡献排名", id: "macronutrient-rankings", style: "section", headlineLevel: 1 });
    content.push({ text: `${data.contributionScope}。按本次记录中各食材提供的营养素量降序排列；同一食材的各餐次摄入合并计算，贡献量相同者并列排名。`,
        fontSize: 8, color: "#56636a", margin: [0, 0, 0, 4] });
    for (const contribution of data.contributions) {
        section(contribution.title, ["排名", "食材", "贡献量"], contribution.rows, [40, "*", 100]);
    }
    section("膳食宝塔比较", ["分类", "本次观察", "参考建议"], data.guidance, [100, "*", "*"]);
    if (data.includeDriReferences) {
        section("DRIs 参考资料", ["营养素", "类型", "采用值", "来源分量与说明"], data.references, [70, 48, 85, "*"],
            "参考类型及单位按原始资料保留；未确定的参考值需人工核定。");
    }
    content.push({ unbreakable: true, stack: [
        { text: "数据来源", style: "section" }, ...data.sources.map(text => ({ text, fontSize: 8, margin: [0, 0, 0, 4] }))
    ] });
    return {
        pageSize: "A4", pageMargins: [40, 40, 40, 82],
        defaultStyle: { font: "ReportSans", fontSize: 9, lineHeight: 1, color: "#202d33" },
        styles: { section: { fontSize: 12, color: "#183b45", margin: [0, 12, 0, 7] } },
        // A4 正文底部留出标题、说明、表头及首行的空间。
        pageBreakBefore: node => node.headlineLevel === 1
            && node.startPosition.top > (node.id === "macronutrient-rankings" ? 575 : 665),
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
