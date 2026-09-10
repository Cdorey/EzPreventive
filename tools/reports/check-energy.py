"""核对能量模板各节段及每页评估水印；输入为 verify-pdf.mjs 的合成成品。"""
import sys
from pathlib import Path
from pypdf import PdfReader

root = Path(sys.argv[1] if len(sys.argv) > 1 else "tmp/reports/energy")
sections = ("总能量核算", "宏量营养素分配", "三餐能量交换份")
for name, included in (
    ("signed", sections), ("evaluation", sections), ("total", sections[:1]),
    ("allocation", sections[1:2]), ("exchanges", sections[2:])
):
    reader = PdfReader(root / f"energy-{name}.pdf")
    text = "\n".join(page.extract_text() for page in reader.pages)
    assert "2000 kcal/日" in text
    for title in sections:
        assert (title in text) == (title in included), (name, title)
    for page in reader.pages:
        page_text = page.extract_text()
        assert ("未经医师审核签发" in page_text) == (name == "evaluation")
        assert "报告编号" in page_text
    assert len(reader.pages) == (2 if name in ("signed", "evaluation") else 1)
    print(f"energy-{name}: {len(reader.pages)} 页，节段与水印通过。")
