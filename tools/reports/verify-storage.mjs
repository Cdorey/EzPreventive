// 使用真实 IndexedDB 和两个浏览器页面验证条件提交；所有内容均为合成字节。
import assert from "node:assert/strict";
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { createRequire } from "node:module";
import { randomUUID } from "node:crypto";
const { chromium } = createRequire(import.meta.url)("playwright");
const source = await readFile(new URL("../../Hosts/EzNutrition.Client/wwwroot/js/archive-storage.js", import.meta.url));
const server = createServer((request, response) => {
    if (request.url === "/archive-storage.js") {
        response.setHeader("Content-Type", "text/javascript");
        response.end(source);
    } else {
        response.end("<!doctype html><title>Local archive transaction test</title>");
    }
});
await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
let browser;
try {
    browser = await chromium.launch({ channel: "msedge", headless: true });
    const context = await browser.newContext();
    const origin = `http://127.0.0.1:${server.address().port}`;
    const pages = await Promise.all([context.newPage(), context.newPage()]);
    await Promise.all(pages.map(page => page.goto(origin)));
    const info = { documentId: randomUUID(), title: "original", lastSavedAt: new Date().toISOString() };
    const commit = (page, content, expected, title = "updated") => page.evaluate(async args => {
        const storage = await import("/archive-storage.js");
        return storage.compareExchangeDocument(args.info, new Uint8Array(args.content),
            args.expected === null ? null : new Uint8Array(args.expected));
    }, { info: { ...info, title }, content, expected });
    assert.equal(await commit(pages[0], [1, 2, 3], null, "original"), true);
    assert.equal(await commit(pages[1], [4], null), false);
    assert.equal(await commit(pages[1], [4], [9]), false);
    const competing = await Promise.all([
        commit(pages[0], [4, 5], [1, 2, 3], "left"),
        commit(pages[1], [6, 7], [1, 2, 3], "right")
    ]);
    assert.equal(competing.filter(Boolean).length, 1);
    const expected = competing[0] ? [4, 5] : [6, 7];
    assert.equal(await commit(pages[0], [8], [1, 2, 3]), false);
    const read = () => pages[0].evaluate(async id => {
        const document = await (await import("/archive-storage.js")).getDocument(id);
        return { info: document.info, content: Array.from(document.content) };
    }, info.documentId);
    const beforeFailure = await read();
    assert.deepEqual(beforeFailure.content, expected);
    assert.equal(beforeFailure.info.title, competing[0] ? "left" : "right");
    const aborted = await pages[0].evaluate(async args => {
        const storage = await import("/archive-storage.js");
        const originalPut = IDBObjectStore.prototype.put;
        IDBObjectStore.prototype.put = function (...values) {
            const result = originalPut.apply(this, values);
            if (this.name === "documentContents") this.transaction.abort();
            return result;
        };
        try {
            await storage.compareExchangeDocument({ ...args.info, title: "must roll back" },
                new Uint8Array([8]), new Uint8Array(args.expected));
            return false;
        } catch { return true; }
        finally { IDBObjectStore.prototype.put = originalPut; }
    }, { info, expected });
    assert.equal(aborted, true);
    assert.deepEqual(await read(), beforeFailure, "事务中止必须同时保留原索引和原正文");
    console.log("IndexedDB：两个页面竞争只有一个提交成功，旧预览被拒绝，中止事务完整保留原文档。");
} finally {
    await browser?.close();
    await new Promise(resolve => server.close(resolve));
}
