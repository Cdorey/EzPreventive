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
const dris = scaleCode === "dris";
const dietary = scaleCode === "dietary";
const sample = samples[scaleCode];
if (!sample && !dris && !dietary) throw new Error("请选择 must、nrs-2002、mna-sf、dris 或 dietary。");
const amend = process.argv[4] === "--revision";
const standalone = process.argv[4] === "--standalone";
if (amend && scaleCode !== "must" && !dietary) throw new Error("更正验收样本使用 MUST 或膳食调查。");
const food = { foodId: randomUUID(), friendlyCode: "test-food", friendlyName: "模拟食物", ediblePortion: 75 };
const nutrients = ["能量", "蛋白质", "脂肪", "碳水化合物", "钾", "钠", "镁", "铁", "锰", "锌", "磷", "硒", "铜",
    "总维生素A", "视黄醇", "胡萝卜素", "硫胺素", "核黄素", "烟酸", "维生素C", "总维生素E"]
    .map((friendlyName, index) => ({ nutrientId: index + 1, friendlyName, defaultMeasureUnit: index === 0 ? "kcal" : index < 4 ? "g" : "mg" }));
const output = `tmp/reports/${scaleCode}${amend ? "-revision" : standalone ? "-standalone" : ""}`;
if (!new Set(["127.0.0.1", "localhost"]).has(new URL(origin).hostname)) throw new Error("仅允许本机测试宿主。");
const expiry = Math.floor(Date.now() / 1000) + 3600;
const sessionId = randomUUID();
const encode = value => Buffer.from(JSON.stringify(value)).toString("base64url");
const token = `${encode({ alg: "none", typ: "JWT" })}.${encode({
    sub: "report-test-doctor", unique_name: "report-test-doctor", sid: sessionId, exp: expiry,
    Permission: standalone || dris ? ["PrintReport"] : ["IssueReport", "PrintReport"], RealName: "模拟医师", InstitutionName: "模拟机构"
})}.`;
const tokens = { sessionId, accessToken: token, accessTokenExpiresAtUtc: new Date(expiry * 1000).toISOString(),
    refreshExpiresAtUtc: new Date((expiry + 3600) * 1000).toISOString(),
    sessionExpiresAtUtc: new Date((expiry + 7200) * 1000).toISOString(), rememberLogin: false };
const browser = await chromium.launch({ channel: "msedge", headless: true });
let page;
let reportPhase = false;
let driOutcome = "full";
const driRecords = [
    { nutrient: "蛋白质", recordType: 1, value: 65, measureUnit: "g" },
    { nutrient: "蛋白质", recordType: 1, value: 15, measureUnit: "g", isOffset: true, detail: "合成调整记录" },
    { nutrient: "脂肪", recordType: 4, value: 20, measureUnit: "%" },
    { nutrient: "脂肪", recordType: 5, value: 30, measureUnit: "%" },
    { nutrient: "钠", recordType: 6, value: 2000, measureUnit: "mg", detail: "合成 PI-NCD 记录" },
    { nutrient: "钾", recordType: 7, value: 3600, measureUnit: "mg" },
    { nutrient: "冲突参考", recordType: 1, value: 10, measureUnit: "mg" },
    { nutrient: "冲突参考", recordType: 1, value: 20, measureUnit: "mg" },
    ...Array.from({ length: 36 }, (_, i) => ({ nutrient: `合成参考${String(i + 1).padStart(2, "0")}`,
        recordType: i % 4, value: i + 1, measureUnit: "mg", detail: "仅用于本机排版与分页验证的合成数据" }))
];
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
        if (dris && path.startsWith("/Energy/DRIs/")) {
            if (driOutcome === "error") return route.fulfill({ status: 503, body: "fixture failure" });
            return fulfill(driOutcome === "empty" ? [] : driRecords);
        }
        if (path.startsWith("/Energy/DRIs/")) return fulfill([{ nutrient: "蛋白质", value: 65, measureUnit: "g", recordType: 0, gender: "女" }]);
        if (path === "/FoodComposition/Foods") return fulfill([food]);
        if (path === "/FoodComposition/Nutrients") return fulfill(dietary ? nutrients : [{ nutrientId: 1, friendlyName: "蛋白质", defaultMeasureUnit: "g" }]);
        if (dietary && path === "/FoodComposition/CompositionData") return fulfill(nutrients.map((nutrient, index) => ({
            nutrientId: nutrient.nutrientId, nutrient, foodId: food.foodId,
            value: [165, 10, 5, 20][index] ?? 1, measureUnit: nutrient.defaultMeasureUnit
        })));
        if (path.startsWith("/Energy/") || path.startsWith("/FoodComposition/")) return fulfill([]);
        if (path.startsWith("/User/")) return fulfill({});
        return route.continue();
    });
    page = await context.newPage();
    page.setDefaultTimeout(30000);
    page.on("pageerror", error => console.error("PAGE ERROR", error.message));
    page.on("console", message => { if (message.type() === "error") console.error("BROWSER ERROR", message.text()); });
    page.on("requestfailed", request => console.error("REQUEST FAILED", request.url(), request.failure()?.errorText));
    if (dris) {
        await page.goto(origin + "/drisinsights");
        await page.getByRole("heading", { name: "DRIs 速查", exact: true }).waitFor();
        if (await page.getByRole("button", { name: "打印评估稿", exact: true }).count())
            throw new Error("未查询时不应出现打印入口。");
        await page.getByText("已知整岁", { exact: true }).click();
        await page.locator("#age input").fill("35");
        await page.locator("#gender").getByText("女", { exact: true }).click();
        await page.getByRole("button", { name: "打印评估稿", exact: true }).waitFor();
        reportPhase = true;
        const popupPromise = context.waitForEvent("page");
        await page.getByRole("button", { name: "打印评估稿", exact: true }).click();
        const popup = await popupPromise;
        await popup.waitForURL(/^blob:/);
        const bytes = await page.evaluate(async url =>
            Array.from(new Uint8Array(await (await fetch(url)).arrayBuffer())), popup.url());
        await mkdir(output, { recursive: true });
        await writeFile(`${output}/evaluation.pdf`, new Uint8Array(bytes));
        await popup.close();
        const records = await page.evaluate(async () => (await import("/js/archive-storage.js")).listDocuments());
        if (records.length) throw new Error("DRIs 打印不应建立档案。");
        await page.screenshot({ path: `${output}/dris-browser.png`, fullPage: true });
        reportPhase = false;
        driOutcome = "empty";
        await page.locator("#gender").getByText("男", { exact: true }).click();
        await page.getByText("暂无适用的膳食参考摄入量", { exact: true }).waitFor();
        if (await page.getByRole("button", { name: "打印评估稿", exact: true }).count())
            throw new Error("空结果仍保留旧打印入口。");
        driOutcome = "error";
        await page.locator("#gender").getByText("女", { exact: true }).click();
        await page.getByText("未能取得适用的参考摄入量", { exact: true }).waitFor();
        if (await page.getByRole("button", { name: "打印评估稿", exact: true }).count())
            throw new Error("查询失败仍保留旧打印入口。");
        console.log("DRIs 多页结果打印、无归档、空结果及失败清除旧输出入口通过。");
    } else if (standalone) {
        await page.goto(`${origin}/assessmentinsights/${scaleCode}`);
        await page.getByText("已知整岁", { exact: true }).click();
        await page.locator("#age input").fill("70");
        await page.locator(".ant-form-item").filter({ hasText: "身高（cm，可选）" }).locator("input").fill("165");
        await page.locator(".ant-form-item").filter({ hasText: "体重（kg，可选）" }).locator("input").fill("60");
        await page.getByRole("button", { name: "开始评估", exact: true }).click();
        const groups = page.locator("fieldset.assessment-item");
        reportPhase = true;
        await mkdir(output, { recursive: true });
        for (const complete of [false, true]) {
            if (complete) {
                for (const [index, answer] of sample.answers.entries())
                    await groups.nth(index).locator(`input[value='${answer}']`).check();
            }
            const popupPromise = context.waitForEvent("page");
            await page.getByRole("button", { name: "打印评估稿", exact: true }).click();
            const popup = await popupPromise;
            await popup.waitForURL(/^blob:/);
            const bytes = await page.evaluate(async url =>
                Array.from(new Uint8Array(await (await fetch(url)).arrayBuffer())), popup.url());
            await writeFile(`${output}/${complete ? "complete" : "incomplete"}.pdf`, new Uint8Array(bytes));
            await popup.close();
        }
        const records = await page.evaluate(async () => (await import("/js/archive-storage.js")).listDocuments());
        if (records.length) throw new Error("独立速查打印不应建立任何咨询或报告档案。");
        if (await page.getByRole("button", { name: "签发报告", exact: true }).count())
            throw new Error("独立速查不应提供签发入口。");
        await page.screenshot({ path: `${output}/standalone-browser.png`, fullPage: true });
        console.log(scaleCode, "独立速查未完成/完整结果打印成功，没有建立档案。");
    } else {
        await page.goto(origin);
        await page.getByRole("button", { name: "开启新咨询" }).click();
        await page.locator("#name").fill("模拟报告患者");
        await page.locator("#gender").getByText("女", { exact: true }).click();
        await page.getByText("已知整岁", { exact: true }).click();
        await page.locator("#age input").fill("70");
        await page.locator(".ant-form-item").filter({ hasText: "身高（cm）" }).locator("input").fill("165");
        await page.locator(".ant-form-item").filter({ hasText: "体重（kg）" }).locator("input").fill("60");
        await page.getByRole("button", { name: "确认并进入核算" }).click();
        let assessment;
        if (dietary) {
            await page.getByRole("tab", { name: "膳食调查", exact: true }).click();
            await page.locator(".food-picker input").click();
            await page.locator(".food-picker input").pressSequentially("模拟", { delay: 150 });
            await page.locator(".ant-select-dropdown:visible").getByText("模拟食物", { exact: true }).click();
            await page.locator(".entry-table").getByRole("spinbutton").fill("200");
            await page.locator(".entry-table").getByRole("switch").click();
            await page.getByRole("button", { name: "计算膳食摄入" }).click();
            await page.getByText("已完成核算", { exact: true }).waitFor();
        } else {
        await page.getByRole("button", { name: "添加量表" }).click();
        await page.locator(".ant-dropdown:visible").getByText(sample.title, { exact: true }).click();
        assessment = page.locator(".assessment-card").filter({ hasText: sample.title });
        // 某些量表在不同题目中复用选项编码，按正式题序定位各自的单选组。
        const groups = assessment.locator("fieldset.assessment-item");
        for (const [index, answer] of sample.answers.entries())
            await groups.nth(index).locator(`input[value='${answer}']`).check();
        }
        reportPhase = true;
        await mkdir(output, { recursive: true });
        await page.getByRole("button", { name: "打印当前评估稿", exact: true }).click();
        const configureDietary = async (showAll, includeDri) => {
            if (!dietary) return;
            await page.getByText("膳食报告设置", { exact: true }).waitFor();
            if (await page.locator("iframe.report-preview").count()) throw new Error("配置确认前已生成预览。");
            const editor = page.locator(".dietary-report-options");
            await page.waitForFunction(() => {
                const modal = document.querySelector(".dietary-report-options")?.closest(".ant-modal");
                return modal && Math.abs(modal.getBoundingClientRect().width - 520) < 2;
            }, undefined, { timeout: 5000 });
            await editor.getByRole("checkbox", { name: "显示全部食材" }).setChecked(false);
            await editor.getByRole("spinbutton").fill("");
            await editor.getByRole("spinbutton").press("Tab");
            await editor.locator("button:disabled").waitFor();
            await editor.getByRole("spinbutton").fill("1");
            await editor.getByRole("checkbox", { name: "显示全部食材" }).setChecked(showAll);
            await editor.getByRole("checkbox", { name: "包含独立的 DRIs 参考资料节" }).setChecked(includeDri);
            await page.locator(".ant-modal-content:visible").screenshot({ path: `${output}/report-options.png` });
            await editor.getByRole("button", { name: "生成预览" }).click();
        };
        const assertPreviewWidth = async () => {
            await page.locator("iframe.report-preview[src^='blob:']").waitFor({ state: "visible" });
            await page.waitForFunction(() => {
                const modal = document.querySelector("iframe.report-preview")?.closest(".ant-modal");
                return modal && Math.abs(modal.getBoundingClientRect().width - 1000) < 2;
            }, undefined, { timeout: 5000 });
        };
        if (dietary) {
            await page.getByText("膳食报告设置", { exact: true }).waitFor();
            await page.getByRole("button", { name: "取消", exact: true }).click();
            if (await page.locator("iframe.report-preview").count()) throw new Error("取消配置留下了预览。");
            await page.getByRole("button", { name: "打印当前评估稿", exact: true }).click();
        }
        await configureDietary(false, false);
        await assertPreviewWidth();
        if (dietary) {
            await page.setViewportSize({ width: 640, height: 900 });
            await page.waitForFunction(() => {
                const frame = document.querySelector("iframe.report-preview");
                const modal = frame?.closest(".ant-modal");
                return modal && modal.getBoundingClientRect().width <= innerWidth
                    && frame.getBoundingClientRect().width <= modal.getBoundingClientRect().width;
            }, undefined, { timeout: 5000 });
            await page.setViewportSize({ width: 1440, height: 1000 });
            await assertPreviewWidth();
            await page.locator(".ant-modal-content").filter({ has: page.locator("iframe.report-preview") })
                .screenshot({ path: `${output}/report-preview-width.png` });
        }
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
        await configureDietary(true, true);
        await assertPreviewWidth();
        await page.locator("iframe.report-preview[src^='blob:']").waitFor({ state: "visible" });
        if (amend) {
            const initialUrl = await page.locator("iframe.report-preview").getAttribute("src");
            const initialBytes = await page.evaluate(async url =>
                Array.from(new Uint8Array(await (await fetch(url)).arrayBuffer())), initialUrl);
            await writeFile(`${output}/browser-initial.pdf`, new Uint8Array(initialBytes));
        }
        await page.getByRole("button", { name: "确认审核并签发" }).click();
        await page.getByText("报告已签发并保存到本机档案库。", { exact: true }).waitFor({ state: "visible" });
        await page.getByRole("button", { name: "关闭", exact: true }).click();
        if (amend) {
            if (dietary) {
                await page.getByRole("button", { name: "修改记录", exact: true }).click();
                await page.locator(".entry-table").getByRole("spinbutton").fill("400");
                await page.getByRole("button", { name: "计算膳食摄入" }).click();
                await page.getByText("已完成核算", { exact: true }).waitFor();
            } else {
            await assessment.locator("input[value='below-18-5']").check();
            }
            await page.getByRole("button", { name: "更正已签发报告", exact: true }).click();
            await page.getByRole("button", { name: /更正第 1 版/ }).click();
            await configureDietary(false, false);
            await assertPreviewWidth();
            await page.locator("iframe.report-preview[src^='blob:']").waitFor({ state: "visible" });
            await page.getByRole("button", { name: "确认更正并签发", exact: true }).click();
            await page.getByText("报告已签发并保存到本机档案库。", { exact: true }).waitFor({ state: "visible" });
            await page.getByRole("button", { name: "关闭", exact: true }).click();
        }
        await page.goto(origin + "/archives");
        await page.getByRole("button", { name: dietary ? "24 小时膳食调查报告" : sample.title + "报告", exact: false }).click();
        await page.getByRole("button", { name: "打印报告原件" }).waitFor({ state: "visible" });
        if (amend) await page.getByText("已被后续签发版本替代；历史原件保留在报告包中。", { exact: true }).waitFor({ state: "visible" });
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
        console.log(scaleCode, "浏览器签发、IndexedDB 归档及刷新后调阅完成：", records.report.documentId);
    }
    const unexpected = reportRequests.filter(request => request.path !== "/archives"
        && !request.path.startsWith("/SystemInfo/") && !request.path.startsWith("/Auth/Browser/"));
    if (unexpected.length || externalRequests.length || reportRequests.some(request => request.body?.includes("模拟报告患者")))
        throw new Error("报告路径存在非预期网络请求。");
    console.log("报告阶段仅有现有认证、公共信息和页面加载请求；已捕获打印窗口中的实际 PDF。");
} catch (error) {
    await mkdir(output, { recursive: true });
    if (page) {
        await page.screenshot({ path: `${output}/browser-failure.png`, fullPage: true });
        await writeFile(`${output}/browser-failure.txt`, await page.locator("body").innerText());
    }
    throw error;
} finally { await browser.close(); }
