const databaseName = "eznutrition-local-archives";
const databaseVersion = 1;
const documentStoreName = "documents";

function requestResult(request) {
    return new Promise((resolve, reject) => {
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error ?? new Error("IndexedDB request failed."));
    });
}

function transactionCompleted(transaction) {
    return new Promise((resolve, reject) => {
        transaction.oncomplete = () => resolve();
        transaction.onabort = () => reject(transaction.error ?? new Error("IndexedDB transaction aborted."));
        transaction.onerror = () => reject(transaction.error ?? new Error("IndexedDB transaction failed."));
    });
}

async function openDatabase() {
    const request = indexedDB.open(databaseName, databaseVersion);
    request.onupgradeneeded = () => {
        const database = request.result;
        if (!database.objectStoreNames.contains(documentStoreName)) {
            const store = database.createObjectStore(documentStoreName, { keyPath: "documentId" });
            store.createIndex("lastSavedAt", "lastSavedAt");
        }
    };
    return await requestResult(request);
}

export async function saveDocument(info, content) {
    const database = await openDatabase();
    try {
        const transaction = database.transaction(documentStoreName, "readwrite");
        const completion = transactionCompleted(transaction);
        transaction.objectStore(documentStoreName).put({
            ...info,
            content: new Uint8Array(content)
        });
        await completion;
    } finally {
        database.close();
    }
}

export async function listDocuments() {
    const database = await openDatabase();
    try {
        const transaction = database.transaction(documentStoreName, "readonly");
        const completion = transactionCompleted(transaction);
        const records = await requestResult(transaction.objectStore(documentStoreName).getAll());
        await completion;
        return records.map(({ content, ...info }) => info);
    } finally {
        database.close();
    }
}

export async function getDocument(documentId) {
    const database = await openDatabase();
    try {
        const transaction = database.transaction(documentStoreName, "readonly");
        const completion = transactionCompleted(transaction);
        const record = await requestResult(transaction.objectStore(documentStoreName).get(documentId));
        await completion;
        if (!record) {
            return null;
        }

        const { content, ...info } = record;
        return { info, content: new Uint8Array(content) };
    } finally {
        database.close();
    }
}

export function openDocument(maximumBytes) {
    return new Promise((resolve, reject) => {
        const input = document.createElement("input");
        input.type = "file";
        input.accept = ".xml,.ezarchive.xml,application/xml,application/vnd.eznutrition.archive+xml";
        input.hidden = true;
        document.body.appendChild(input);

        const removeInput = () => input.remove();
        input.addEventListener("cancel", () => {
            removeInput();
            resolve(null);
        }, { once: true });
        input.addEventListener("change", async () => {
            try {
                const file = input.files?.[0];
                if (!file) {
                    resolve(null);
                    return;
                }

                if (file.size > maximumBytes) {
                    throw new Error("Archive document exceeds the configured size limit.");
                }

                resolve({
                    fileName: file.name,
                    mediaType: file.type || "application/xml",
                    content: new Uint8Array(await file.arrayBuffer())
                });
            } catch (error) {
                reject(error);
            } finally {
                removeInput();
            }
        }, { once: true });
        input.click();
    });
}

export function downloadDocument(suggestedFileName, mediaType, content) {
    const blob = new Blob([new Uint8Array(content)], { type: mediaType });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = suggestedFileName;
    anchor.hidden = true;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}
