import { test } from "node:test";
import assert from "node:assert/strict";
import * as auth from "../../../Hosts/EzNutrition.Client/wwwroot/auth-session.mjs";

const baseAddress = "https://app.example.test/clinic/";
const stateKey = "EzNutrition.Auth:" + baseAddress + ":state";

/** 提供共享 Cookie、存储及排他锁，使多标签页的真实模块代码在受控网络下运行。 */
function environment(t) {
    const values = new Map();
    const listeners = new Set();
    const queues = new Map();
    const requests = [];
    let cookie = null;
    let nextSession = 0;
    const server = { offline: false, csrfAccepted: true, refreshCount: 0, requests, values, listeners };
    const globals = {
        location: new URL(baseAddress),
        localStorage: {
            getItem: key => values.get(key) ?? null,
            setItem: (key, value) => values.set(key, String(value)),
            removeItem: key => values.delete(key)
        },
        navigator: {
            locks: {
                request(name, action) {
                    const result = (queues.get(name) ?? Promise.resolve()).then(action);
                    queues.set(name, result.catch(() => {}));
                    return result;
                }
            }
        },
        addEventListener: (name, callback) => { assert.equal(name, "storage"); listeners.add(callback); },
        removeEventListener: (name, callback) => { assert.equal(name, "storage"); listeners.delete(callback); },
        fetch: async (url, options) => {
            const action = new URL(url).pathname.split("/").at(-1);
            requests.push({ url: String(url), action, options });
            if (server.offline) throw new TypeError("simulated offline");
            assert.equal(options.credentials, "same-origin");
            assert.equal(options.redirect, "error");
            assert.equal(options.cache, "no-store");
            if (action === "Csrf") {
                return new Response(JSON.stringify({ requestToken: "csrf-token" }), { status: server.csrfAccepted ? 200 : 400 });
            }
            assert.equal(options.method, "POST");
            assert.equal(options.headers["X-CSRF-TOKEN"], "csrf-token");
            const body = JSON.parse(options.body);
            assert.equal(new URL(url).pathname, "/clinic/Auth/Browser/" + action);
            if (action === "Login") {
                cookie = { sessionId: "session-" + ++nextSession, version: 0 };
            } else if (action === "Logout") {
                if (cookie && body.sessionId && cookie.sessionId !== body.sessionId) {
                    return error(409, "session_changed");
                }
                cookie = null;
                return new Response(null, { status: 204 });
            } else {
                assert.equal(action, "Refresh");
                server.refreshCount++;
                if (!cookie) return new Response(null, { status: 204 });
                if (body.sessionId && cookie.sessionId !== body.sessionId) return error(409, "session_changed");
                const sentCookie = cookie;
                await new Promise(resolve => queueMicrotask(resolve));
                if (sentCookie !== cookie) return error(401, "session_invalid");
                cookie = { sessionId: cookie.sessionId, version: cookie.version + 1 };
            }
            return new Response(JSON.stringify({
                sessionId: cookie.sessionId,
                accessToken: "access-secret-" + cookie.sessionId + "-" + cookie.version,
                rememberLogin: true
            }));
        }
    };
    for (const [name, value] of Object.entries(globals)) {
        const original = Object.getOwnPropertyDescriptor(globalThis, name);
        Object.defineProperty(globalThis, name, { value, writable: true, configurable: true });
        t.after(() => {
            if (original) Object.defineProperty(globalThis, name, original);
            else delete globalThis[name];
        });
    }
    return server;
}

function error(status, code) {
    return new Response(JSON.stringify({ code, message: "test failure" }), { status });
}

function login() {
    return auth.login(baseAddress, { userName: "user", password: "password-secret", rememberLogin: true });
}

test("login uses CSRF and same-origin cookies without storing credentials", async t => {
    const server = environment(t);
    const result = await login();
    assert.equal(result.status, 200);
    assert.equal(result.tokens.refreshToken, undefined);
    assert.equal(server.requests.length, 2);
    const persisted = [...server.values.values()].join();
    assert.doesNotMatch(persisted, /access-secret|refreshToken|password-secret/);
    assert.equal(JSON.parse(server.values.get(stateKey)).sessionId, result.tokens.sessionId);
});

test("concurrent tabs serialize refresh against the newest cookie", async t => {
    const server = environment(t);
    const signedIn = await login();
    const request = { sessionId: signedIn.tokens.sessionId };
    const results = await Promise.all([
        auth.refresh(baseAddress, request), auth.restore(baseAddress),
        auth.refresh(baseAddress, request), auth.restore(baseAddress)
    ]);
    assert.ok(results.every(result => result.status === 200));
    assert.equal(new Set(results.map(result => result.tokens.accessToken)).size, 4);
    assert.equal(server.refreshCount, 4);
});

test("a stale tab cannot refresh or log out a newer account", async t => {
    const server = environment(t);
    const old = await login();
    const current = await login();
    const count = server.requests.length;
    const refresh = await auth.refresh(baseAddress, { sessionId: old.tokens.sessionId });
    assert.equal(refresh.status, 409);
    assert.equal(refresh.error.code, "session_changed");
    assert.equal((await auth.logout(baseAddress, { sessionId: old.tokens.sessionId })).status, 204);
    assert.equal(server.requests.length, count);
    assert.equal(JSON.parse(server.values.get(stateKey)).sessionId, current.tokens.sessionId);
});

test("offline logout blocks restoration until its revocation is completed", async t => {
    const server = environment(t);
    const signedIn = await login();
    server.offline = true;
    assert.equal((await auth.logout(baseAddress, { sessionId: signedIn.tokens.sessionId })).status, 503);
    assert.equal(JSON.parse(server.values.get(stateKey)).kind, "logout");
    assert.equal((await auth.restore(baseAddress)).status, 503);
    assert.equal(server.refreshCount, 0);
    server.offline = false;
    assert.equal((await auth.restore(baseAddress)).status, 204);
    assert.equal(server.values.has("EzNutrition.Auth:" + baseAddress + ":logout"), false);
    assert.equal(server.refreshCount, 1);
    const posts = server.requests.filter(request => request.options.method === "POST");
    assert.deepEqual(posts.map(request => request.action), ["Login", "Logout", "Refresh"]);
});

test("failed CSRF bootstrap never sends login credentials", async t => {
    const server = environment(t);
    server.csrfAccepted = false;
    assert.equal((await login()).status, 503);
    assert.deepEqual(server.requests.map(request => request.action), ["Csrf"]);
    assert.equal(server.values.size, 0);
});

test("cross-origin authentication and missing Web Locks fail before sending credentials", async t => {
    const server = environment(t);
    assert.equal((await auth.login("https://outside.example.test/", {})).status, 503);
    navigator.locks = null;
    assert.equal((await login()).status, 503);
    assert.equal(server.requests.length, 0);
});

test("cross-tab notifications contain no tokens and subscriptions are removable", async t => {
    const server = environment(t);
    const calls = [];
    auth.subscribe(baseAddress, { invokeMethodAsync: async name => { calls.push(name); } });
    for (const listener of server.listeners) {
        listener({ key: "unrelated" });
        listener({ key: stateKey });
    }
    assert.deepEqual(calls, ["NotifySessionChanged"]);
    auth.unsubscribe(baseAddress);
    assert.equal(server.listeners.size, 0);
});
