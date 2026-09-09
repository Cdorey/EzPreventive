// 仅连接本机开发宿主；认证和参考数据全部由测试拦截器提供，不使用真实账号或患者。
import { createRequire } from "node:module";
import { mkdir, writeFile } from "node:fs/promises";
import { randomUUID } from "node:crypto";
const { chromium } = createRequire(import.meta.url)("playwright");
const origin = process.argv[2] || "http://127.0.0.1:5186";
const scaleCode = process.argv[3] || "must";
const samples = {
    must: { title: "营养不良通用筛查工具 MUST", answers: ["above-20", "below-five-percent", "absent"] },
    "nrs-2002": { title: "临床营养风险筛查 NRS 2002", answers: ["bmi-at-least-18-5", "no-scored-weight-loss", "no-scored-intake-reduction", "no-scored-disease-severity"] },
    "mna-sf": { title: "微营养评定法（简表）MNA-SF", answers: ["unchanged", "none", "goes-out", "no", "none"] }
};
const sample = samples[scaleCode];
if (!sample) throw new Error("请选择 must、nrs-2002 或 mna-sf。");
const output = `tmp/reports/${scaleCode}`;
if (!new Set(["127.0.0.1", "localhost"]).has(new URL(origin).hostname)) throw new Error("仅允许本机测试宿主。");
const expiry = Math.floor(Date.now() / 1000) + 3600;
const sessionId = randomUUID();
const encode = value => Buffer.from(JSON.stringify(value)).toString("base64url");
const token = `${encode({ alg: "none", typ: "JWT" })}.${encode({
    sub: "report-test-doctor", unique_name: "report-test-doctor", sid: sessionId, exp: expiry,
    Permission: ["IssueReport", "PrintReport"], RealName: "模拟医师", InstitutionName: "模拟机构"
})}.`;
const tokens = { sessionId, accessToken: token, accessTokenExpiresAtUtc: new Date(expiry * 1000).toISOString(),
    refreshExpiresAtUtc: new Date((expiry + 3600) * 1000).toISOString(),
    sessionExpiresAtUtc: new Date((expiry + 7200) * 1000).toISOString(), rememberLogin: false };
const browser = await chromium.launch({ channel: "msedge", headless: true });
let page;
let reportPhase = false;
const reportRequests = [];
const externalRequests = [];
try {
    const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
    await context.route("**/*", async route => {
        const request = route.request();
        const url = new URL(request.url());
        if (url.origin !== origin) { externalRequests.push(url.href); return route.abort(); }
        const path = url.pathname;
        const fulfill = data => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(data) });
        if (reportPhase && !path.startsWith("/_") && !/\.(js|mjs|css|otf|png|ico|json|wasm)$/.test(path))
            reportRequests.push({ path, method: request.method(), body: request.postData() });
        if (path === "/Auth/Browser/Csrf") return fulfill({ requestToken: "local-test" });
        if (path.startsWith("/Auth/Browser/")) return fulfill(tokens);
        if (path.startsWith("/SystemInfo/")) return fulfill({ caseNumber: "test", serverVersion: "test", description: "本机测试" });
        if (path.startsWith("/Energy/DRIs/")) return fulfill([{ nutrient: "蛋白质", value: 65, measureUnit: "g", recordType: 0, gender: "女" }]);
        if (path === "/FoodComposition/Foods") return fulfill([{ foodId: randomUUID(), friendlyCode: "test-food", name: "模拟食物" }]);
        if (path === "/FoodComposition/Nutrients") return fulfill([{ nutrientId: 1, name: "蛋白质", defaultMeasureUnit: "g" }]);
        if (path.startsWith("/Energy/") || path.startsWith("/FoodComposition/")) return fulfill([]);
        if (path.startsWith("/User/")) return fulfill({});
        return route.continue();
    });
    page = await context.newPage();
    page.setDefaultTimeout(30000);
    page.on("pageerror", error => console.error("PAGE ERROR", error.message));
    await page.goto(origin);
    await page.getByRole("button", { name: "开启新咨询" }).click();
    await page.locator("#name").fill("模拟报告患者");
    await page.locator("#gender").getByText("女", { exact: true }).click();
    await page.getByText("已知整岁", { exact: true }).click();
    await page.locator("#age input").fill("70");
    await page.locator(".ant-form-item").filter({ hasText: "身高（cm）" }).locator("input").fill("165");
    await page.locator(".ant-form-item").filter({ hasText: "体重（kg）" }).locator("input").fill("60");
    await page.getByRole("button", { name: "确认并进入核算" }).click();
    await page.getByRole("button", { name: "添加量表" }).click();
    await page.locator(".ant-dropdown:visible").getByText(sample.title, { exact: true }).click();
    const assessment = page.locator(".assessment-card").filter({ hasText: sample.title });
    // 某些量表在不同题目中复用选项编码，按正式题序定位各自的单选组。
    const groups = assessment.locator("fieldset.assessment-item");
    for (const [index, answer] of sample.answers.entries())
        await groups.nth(index).locator(`input[value='${answer}']`).check();
    reportPhase = true;
    await mkdir(output, { recursive: true });
    await page.getByRole("button", { name: "打印当前评估稿", exact: true }).click();
    await page.locator("iframe.report-preview[src^='blob:']").waitFor({ state: "visible" });
    const evaluationPopupPromise = context.waitForEvent("page");
    await page.getByRole("button", { name: "打开打印窗口", exact: true }).click();
    const evaluationPopup = await evaluationPopupPromise;
    await evaluationPopup.waitForURL(/^blob:/);
    const evaluationBytes = await page.evaluate(async url =>
        Array.from(new Uint8Array(await (await fetch(url)).arrayBuffer())), evaluationPopup.url());
    await writeFile(`${output}/browser-evaluation.pdf`, new Uint8Array(evaluationBytes));
    await evaluationPopup.close();
    await page.getByRole("button", { name: "关闭", exact: true }).click();
    const beforeIssue = await page.evaluate(async () => (await import("/js/archive-storage.js")).listDocuments());
    if (beforeIssue.some(record => record.formatIdentifier.endsWith("report-package")))
        throw new Error("评估打印不应建立正式报告档案。");
    await page.getByRole("button", { name: "签发报告", exact: true }).click();
    await page.locator("iframe.report-preview[src^='blob:']").waitFor({ state: "visible" });
    await page.getByRole("button", { name: "确认审核并签发" }).click();
    await page.getByText("报告已签发并保存到本机档案库。", { exact: true }).waitFor({ state: "visible" });
    await page.getByRole("button", { name: "关闭", exact: true }).click();
    await page.goto(origin + "/archives");
    await page.getByRole("button", { name: sample.title + "报告", exact: false }).click();
    await page.getByRole("button", { name: "打印报告原件" }).waitFor({ state: "visible" });
    const records = await page.evaluate(async () => {
        const storage = await import("/js/archive-storage.js");
        const records = await storage.listDocuments();
        const report = records.find(record => record.formatIdentifier.endsWith("report-package"));
        const document = await storage.getDocument(report.documentId);
        return { report, bytes: Array.from(document.content) };
    });
    await writeFile(`${output}/browser-issued.ezreport`, new Uint8Array(records.bytes));
    const popupPromise = context.waitForEvent("page");
    await page.getByRole("button", { name: "打印报告原件" }).click();
    const popup = await popupPromise;
    await popup.waitForURL(/^blob:/);
    const printedBytes = await page.evaluate(async url =>
        Array.from(new Uint8Array(await (await fetch(url)).arrayBuffer())), popup.url());
    await writeFile(`${output}/browser-printed.pdf`, new Uint8Array(printedBytes));
    await popup.close();
    await page.screenshot({ path: `${output}/archive-browser.png`, fullPage: true });
    const unexpected = reportRequests.filter(request => request.path !== "/archives"
        && !request.path.startsWith("/SystemInfo/") && !request.path.startsWith("/Auth/Browser/"));
    if (unexpected.length || externalRequests.length || reportRequests.some(request => request.body?.includes("模拟报告患者")))
        throw new Error("报告路径存在非预期网络请求。");
    console.log(scaleCode, "浏览器签发、IndexedDB 归档及刷新后调阅完成：", records.report.documentId);
    console.log("报告阶段仅有现有认证、公共信息和页面加载请求；已捕获打印窗口中的实际 PDF。");
} catch (error) {
    await mkdir(output, { recursive: true });
    if (page) {
        await page.screenshot({ path: `${output}/browser-failure.png`, fullPage: true });
        await writeFile(`${output}/browser-failure.txt`, await page.locator("body").innerText());
    }
    throw error;
} finally { await browser.close(); }
