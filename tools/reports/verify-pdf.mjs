// 用真实浏览器执行生产 PDF 模板；全部内容均为合成样本。
import { createServer } from "node:http";
import { readFile, mkdir, writeFile } from "node:fs/promises";
import { resolve, dirname, extname, sep } from "node:path";
import { fileURLToPath } from "node:url";
import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");
const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../Libraries/EzNutrition.Presentation/wwwroot");
const output = resolve(process.argv[2] || "tmp/reports");
const server = createServer(async (request, response) => {
    const url = new URL(request.url, "http://localhost");
    if (url.pathname === "/") {
        response.setHeader("Content-Type", "text/html");
        response.end("<!doctype html><html><head><meta charset='utf-8'></head><body>PDF test</body></html>");
        return;
    }
    const path = resolve(root, "." + decodeURIComponent(url.pathname));
    if (!path.startsWith(root + sep)) { response.writeHead(403).end(); return; }
    try {
        response.setHeader("Content-Type", extname(path) === ".otf" ? "font/otf" : "text/javascript");
        response.end(await readFile(path));
    } catch { response.writeHead(404).end(); }
});
await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
let browser;
try {
    browser = await chromium.launch({ channel: "msedge", headless: true });
    const page = await browser.newPage();
    const origin = `http://127.0.0.1:${server.address().port}`;
    const externalRequests = [];
    await page.route("**/*", route => {
        if (!route.request().url().startsWith(origin + "/")) {
            externalRequests.push(route.request().url());
            return route.abort();
        }
        return route.continue();
    });
    await page.goto(origin);
    await mkdir(output, { recursive: true });
    const sample = {
        title: "营养不良通用筛查工具 MUST 报告",
        reportNumber: "00000000-0000-0000-0000-000000000001",
        instrumentVersion: "BAPEN MUST", patientName: "模拟患者·长姓名测试", sex: "女", age: "70岁",
        height: "165 cm", weight: "60 kg", assessedAt: "2026-09-09 10:00:00 +08:00", performer: "模拟评估员",
        totalScore: "0 分", interpretation: "营养不良低风险",
        responses: [["Step 1：BMI 评分", "BMI >20 kg/m²（BMI >30 kg/m² 时同时记录肥胖）", "0"],
            ["Step 2：过去 3～6 个月非计划性体重下降", "<5%", "0"],
            ["Step 3：急性疾病影响", "不符合急性疾病影响条件", "0"]],
        results: [["BMI 评分", "0"], ["非计划性体重下降评分", "0"], ["急性疾病影响评分", "0"]],
        isEvaluation: false, signer: "模拟医师", institution: "模拟机构（非真实医疗机构）",
        reportTime: "2026-09-09 10:05:00 +08:00"
    };
    for (const [name, data] of [
        ["signed", sample],
        ["evaluation", { ...sample, isEvaluation: true }],
        ["multipage", { ...sample, isEvaluation: true,
            interpretation: "此处为长结论分页测试。".repeat(45),
            responses: Array.from({ length: 45 }, (_, index) => [
                `模拟项目 ${index + 1}`, "模拟长回答，验证换行、分页以及表头重复。".repeat(5), "0"])
        }]
    ]) {
        const bytes = await page.evaluate(async data => {
            const { render } = await import("/reports/assessment-report.mjs");
            return Array.from(await render(data));
        }, data);
        await writeFile(resolve(output, `${name}.pdf`), new Uint8Array(bytes));
        console.log(`${name}: ${bytes.length} bytes`);
    }
    if (externalRequests.length) throw new Error("发现外部请求：" + externalRequests.join(", "));
    console.log("全部 PDF 由本地资源生成，无外部网络请求。");
} finally {
    await browser?.close();
    await new Promise(resolve => server.close(resolve));
}
