// 刷新凭据始终由 HttpOnly Cookie 携带；本模块不读取或保存其内容。
const subscriptions = new Map();

function scopeFor(baseAddress) {
    const base = new URL(baseAddress);
    if (base.origin !== location.origin || !base.pathname.endsWith("/")) {
        throw new Error("认证接口必须与当前页面同源。");
    }
    return { base, key: "EzNutrition.Auth:" + base.href };
}

function announce(scope, sessionId, kind) {
    // 仅保存非秘密的会话标识和变更通知，不包含任何访问或刷新令牌。
    localStorage.setItem(scope.key + ":state", JSON.stringify({
        sessionId, kind, revision: crypto.randomUUID()
    }));
}

function readState(scope) {
    const value = localStorage.getItem(scope.key + ":state");
    return value ? JSON.parse(value) : null;
}

function unavailable(message = "暂时无法连接认证服务，请检查网络后重试。") {
    return { status: 503, error: { code: "temporarily_unavailable", message } };
}

async function locked(baseAddress, action) {
    try {
        const scope = scopeFor(baseAddress);
        if (!navigator.locks) {
            return unavailable("当前浏览器不支持安全的跨窗口会话协调，请更新浏览器。");
        }
        return await navigator.locks.request(scope.key, () => action(scope));
    } catch {
        return unavailable();
    }
}

async function post(scope, action, body) {
    // 同一个超时覆盖防伪令牌取得和实际请求；不自动重发一次性刷新操作。
    const signal = AbortSignal.timeout(15000);
    const options = { credentials: "same-origin", cache: "no-store", redirect: "error", signal };
    const csrf = await fetch(new URL("Auth/Browser/Csrf", scope.base), options);
    if (!csrf.ok) {
        return unavailable("无法完成页面安全校验，请重新加载后重试。");
    }
    const { requestToken } = await csrf.json();
    if (typeof requestToken !== "string" || requestToken.length === 0) {
        return unavailable("认证服务返回了无效的页面安全凭据。");
    }
    const response = await fetch(new URL("Auth/Browser/" + action, scope.base), {
        ...options,
        method: "POST",
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": requestToken },
        body: JSON.stringify(body)
    });
    if (response.status === 204) {
        return { status: 204 };
    }
    let data;
    try {
        data = await response.json();
    } catch {
        return unavailable("认证服务返回了无法识别的响应，请稍后重试。");
    }
    if (response.ok) {
        return { status: 200, tokens: data };
    }
    return {
        status: response.status,
        error: response.status === 401 || response.status === 409
            ? data
            : { code: "temporarily_unavailable", message: "认证请求未完成，请稍后重试或重新加载页面。" }
    };
}

async function finishPendingLogout(scope) {
    const pending = localStorage.getItem(scope.key + ":logout");
    if (!pending) {
        return null;
    }
    const result = await post(scope, "Logout", JSON.parse(pending));
    if (result.status === 204 || result.status === 409) {
        localStorage.removeItem(scope.key + ":logout");
        return null;
    }
    return result;
}

/** 登录、轮换及退出共用浏览器跨标签页锁。 */
export function login(baseAddress, request) {
    return locked(baseAddress, async scope => {
        const pending = await finishPendingLogout(scope);
        if (pending) return pending;
        const result = await post(scope, "Login", request);
        if (result.status === 200) {
            announce(scope, result.tokens.sessionId, "login");
        }
        return result;
    });
}

async function refreshUnlocked(scope, request) {
    const pending = await finishPendingLogout(scope);
    if (pending) return pending;
    const known = readState(scope);
    if (request.sessionId && known?.sessionId && request.sessionId !== known.sessionId) {
        return {
            status: 409,
            error: { code: "session_changed", message: "登录状态已在其他窗口改变，请重新确认当前账号。" }
        };
    }
    const result = await post(scope, "Refresh", request);
    if (result.status === 200 && known?.sessionId !== result.tokens.sessionId) {
        announce(scope, result.tokens.sessionId, "login");
    } else if (result.status === 401 || (result.status === 204 && known?.sessionId)) {
        announce(scope, null, "logout");
    }
    return result;
}

/** 页面重载时尝试恢复 Cookie；未保存会话时正常返回空结果。 */
export function restore(baseAddress) {
    return locked(baseAddress, scope => refreshUnlocked(scope, {}));
}

/** 用预期会话标识阻止旧请求在其他账号下继续执行。 */
export function refresh(baseAddress, request) {
    return locked(baseAddress, scope => refreshUnlocked(scope, request));
}

/** 离线退出先记录意图，重载后优先完成注销，避免残留 Cookie 自动恢复登录。 */
export function logout(baseAddress, request) {
    return locked(baseAddress, async scope => {
        const known = readState(scope);
        if (request.sessionId && known?.sessionId && request.sessionId !== known.sessionId) {
            return { status: 204 };
        }
        const target = { sessionId: request.sessionId ?? known?.sessionId ?? null };
        localStorage.setItem(scope.key + ":logout", JSON.stringify(target));
        announce(scope, null, "logout");
        const pending = await finishPendingLogout(scope);
        return pending ?? { status: 204 };
    });
}

/** 监听不含令牌的跨标签页登录通知。 */
export function subscribe(baseAddress, dotnet) {
    unsubscribe(baseAddress);
    const scope = scopeFor(baseAddress);
    const handler = event => {
        if (event.key === scope.key + ":state") {
            void dotnet.invokeMethodAsync("NotifySessionChanged").catch(() => {
                console.error("未能同步其他窗口的登录状态。");
            });
        }
    };
    subscriptions.set(baseAddress, handler);
    addEventListener("storage", handler);
}

/** 释放页面的跨标签页监听器。 */
export function unsubscribe(baseAddress) {
    const handler = subscriptions.get(baseAddress);
    if (handler) {
        removeEventListener("storage", handler);
        subscriptions.delete(baseAddress);
    }
}
