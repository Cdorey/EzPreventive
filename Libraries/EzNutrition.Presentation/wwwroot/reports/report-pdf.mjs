export const evaluationNotice = "仅供教学或功能评估使用 · 未经医师审核签发";
let enginePromise;

async function engine() {
    if (!enginePromise) {
        enginePromise = import("./vendor/pdfmake.min.js").then(() => {
            const pdfMake = globalThis.pdfMake;
            if (!pdfMake) throw new Error("本机 PDF 生成组件加载失败。");
            const font = new URL("./fonts/NotoSansCJKsc-Regular.otf", import.meta.url).href;
            pdfMake.addFonts({ ReportSans: { normal: font, bold: font, italics: font, bolditalics: font } });
            return pdfMake;
        }).catch(error => {
            enginePromise = undefined;
            throw error;
        });
    }
    return enginePromise;
}

export function table(headers, rows, widths) {
    return {
        table: {
            headerRows: 1,
            dontBreakRows: true,
            widths,
            body: [headers.map(text => ({ text, fillColor: "#eef2f3", color: "#183b45" })), ...rows]
        },
        layout: {
            hLineWidth: () => 0.4,
            vLineWidth: () => 0,
            hLineColor: () => "#d2dadc",
            paddingLeft: () => 7,
            paddingRight: () => 7,
            paddingTop: () => 3,
            paddingBottom: () => 3
        }
    };
}

/** 评估用途始终进入成品，不依赖打印机的背景色设置。 */
export function evaluationWatermark() {
    return { text: "教学 / 功能评估 · 未经医师审核", font: "ReportSans", color: "#7c8790", opacity: 0.16, fontSize: 25, angle: -32 };
}

/** 使用随应用发布的字体和引擎生成 PDF 字节。 */
export async function renderPdf(definition) {
    const pdfMake = await engine();
    return new Uint8Array(await pdfMake.createPdf(definition).getBuffer());
}
