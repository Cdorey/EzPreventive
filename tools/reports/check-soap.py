"""核对 SOAP 实际模板的完整文本、空节和长文分页。"""
import sys
from pathlib import Path
from pypdf import PdfReader

root = Path(sys.argv[1] if len(sys.argv) > 1 else "tmp/reports/soap")
for name in ("signed", "evaluation", "empty-sections", "long"):
    reader = PdfReader(root / f"soap-{name}.pdf")
    text = "\n".join(page.extract_text() for page in reader.pages)
    assert "SOAP 咨询记录报告" in text
    for title in ("S · 主观资料", "O · 客观资料", "A · 问题评估", "P · 处理计划"):
        assert title in text
    for page in reader.pages:
        content = page.extract_text()
        assert "报告编号" in content
        assert ("未经医师审核签发" in content) == (name in ("evaluation", "long"))
    if name == "empty-sections":
        assert "未记录" not in text
        assert "合成主观资料" not in text and "合成处理计划" in text
    elif name == "long":
        assert len(reader.pages) > 1
        assert text.count("40. 合成记录") == 4
    else:
        assert "合成主观资料\n第二行记录" in text
        assert "合成客观资料" in text and "合成问题评估" in text and "合成处理计划" in text
    print(f"soap-{name}: {len(reader.pages)} 页，内容、空节和水印检查通过。")
