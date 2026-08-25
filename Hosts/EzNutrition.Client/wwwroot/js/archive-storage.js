const databaseName = "eznutrition-local-archives";
const databaseVersion = 2;
const documentStoreName = "documents";
const documentContentStoreName = "documentContents";

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
    request.onupgradeneeded = event => {
        const database = request.result;
        const transaction = request.transaction;
        const documentStore = database.objectStoreNames.contains(documentStoreName)
            ? transaction.objectStore(documentStoreName)
            : database.createObjectStore(documentStoreName, { keyPath: "documentId" });
        if (!documentStore.indexNames.contains("lastSavedAt")) {
            documentStore.createIndex("lastSavedAt", "lastSavedAt");
        }

        const contentStore = database.objectStoreNames.contains(documentContentStoreName)
            ? transaction.objectStore(documentContentStoreName)
            : database.createObjectStore(documentContentStoreName);

        if (event.oldVersion < 2) {
            migrateInlineDocumentContent(documentStore, contentStore);
        }
    };
    return await requestResult(request);
}

function migrateInlineDocumentContent(documentStore, contentStore) {
    const request = documentStore.openCursor();
    request.onsuccess = () => {
        const cursor = request.result;
        if (!cursor) {
            return;
        }

        const record = cursor.value;
        if (record.content !== undefined) {
            const { content, ...info } = record;
            contentStore.put(new Uint8Array(content), record.documentId);
            cursor.update(info);
        }
        cursor.continue();
    };
}

export async function saveDocument(info, content) {
    const database = await openDatabase();
    try {
        const transaction = database.transaction(
            [documentStoreName, documentContentStoreName],
            "readwrite");
        const completion = transactionCompleted(transaction);
        transaction.objectStore(documentStoreName).put(info);
        transaction.objectStore(documentContentStoreName).put(
            new Uint8Array(content),
            info.documentId);
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
        return records;
    } finally {
        database.close();
    }
}

export async function getDocument(documentId) {
    const database = await openDatabase();
    try {
        const transaction = database.transaction(
            [documentStoreName, documentContentStoreName],
            "readonly");
        const completion = transactionCompleted(transaction);
        const [info, content] = await Promise.all([
            requestResult(transaction.objectStore(documentStoreName).get(documentId)),
            requestResult(transaction.objectStore(documentContentStoreName).get(documentId))
        ]);
        await completion;
        if (!info) {
            return null;
        }
        if (content === undefined) {
            throw new Error("Archive document content is missing.");
        }

        return { info, content: new Uint8Array(content) };
    } finally {
        database.close();
    }
}

export async function deleteDocument(documentId) {
    const database = await openDatabase();
    try {
        const transaction = database.transaction(
            [documentStoreName, documentContentStoreName],
            "readwrite");
        const completion = transactionCompleted(transaction);
        transaction.objectStore(documentStoreName).delete(documentId);
        transaction.objectStore(documentContentStoreName).delete(documentId);
        await completion;
    } finally {
        database.close();
    }
}

export async function clearDocuments() {
    const database = await openDatabase();
    try {
        const transaction = database.transaction(
            [documentStoreName, documentContentStoreName],
            "readwrite");
        const completion = transactionCompleted(transaction);
        transaction.objectStore(documentStoreName).clear();
        transaction.objectStore(documentContentStoreName).clear();
        await completion;
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
