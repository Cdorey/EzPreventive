export function getSelectedTextWithin(container) {
    if (!container) {
        return "";
    }

    const selection = window.getSelection();
    if (!selection || selection.isCollapsed || selection.rangeCount === 0) {
        return "";
    }

    for (let index = 0; index < selection.rangeCount; index += 1) {
        const range = selection.getRangeAt(index);
        if (!container.contains(range.startContainer)
            || !container.contains(range.endContainer)) {
            return "";
        }
    }

    return selection.toString();
}
