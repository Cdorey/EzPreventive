"""检查真实模板输出的页数、文本与每页标记；布局仍需配合渲染图检查。"""
from pathlib import Path
from pypdf import PdfReader

output = Path("tmp/reports")
notice = "仅供教学或功能评估使用"
for name in ("signed", "evaluation", "multipage"):
    pages = PdfReader(output / f"{name}.pdf").pages
    assert len(pages) == 1 if name != "multipage" else len(pages) > 1
    text = "\n".join(page.extract_text() for page in pages)
    assert "MUST" in text and "模拟" in text
    if name == "signed":
        assert "审核签发：模拟医师" in text
        assert notice not in text and "未经医师审核" not in text
    else:
        for page in pages:
            assert notice in page.extract_text()
            assert "未经医师审核" in page.extract_text()
    for page in pages:
        assert "00000000-0000-0000-0000-000000000001" in page.extract_text()
        # 原件必须携带字体，重印不能依赖另一台设备安装同名中文字体。
        fonts = page["/Resources"]["/Font"].get_object().values()
        for reference in fonts:
            font = reference.get_object()
            descendants = font.get("/DescendantFonts", [font])
            for descendant in descendants:
                descriptor = descendant.get_object()["/FontDescriptor"].get_object()
                embedded = next((descriptor[key] for key in ("/FontFile", "/FontFile2", "/FontFile3") if key in descriptor), None)
                assert embedded is not None and embedded.get_object().get_data(), "PDF 缺少嵌入字体"
    if name == "multipage":
        for number in range(1, 46):
            assert f"模拟项目 {number}" in text
    print(f"{name}: {len(pages)} pages, text and page markers verified")
