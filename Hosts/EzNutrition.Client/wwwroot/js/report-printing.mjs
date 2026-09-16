/** 打开 PDF 原件，不对工作台页面调用 window.print。打印由 PDF 查看器提供。 */
export function openForPrinting(bytes) {
    const viewer = window.open("about:blank", "_blank");
    if (!viewer) throw new Error("浏览器阻止了报告窗口，请允许本站打开新窗口后重试。");
    const url = URL.createObjectURL(new Blob([bytes], { type: "application/pdf" }));
    viewer.opener = null;
    viewer.location.replace(url);
    // 查看器可能要很久才打印；保留其原件，直至窗口关闭。
    const timer = window.setInterval(() => {
        if (viewer.closed) {
            URL.revokeObjectURL(url);
            window.clearInterval(timer);
        }
    }, 1000);
}
