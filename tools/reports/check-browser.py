"""核对真实浏览器签发包与打印窗口中的原件完全一致。"""
import hashlib
import sys
from pathlib import Path
import zipfile
from pypdf import PdfReader

scale = sys.argv[1] if len(sys.argv) > 1 else "must"
expected = {
    "must": ("MUST", "0 分", "营养不良低风险"),
    "nrs-2002": ("NRS 2002", "1 分", "目前没有营养风险"),
    "mna-sf": ("MNA-SF", "13 分", "未提示营养不良风险"),
}[scale]
root = Path("tmp/reports") / scale
with zipfile.ZipFile(root / "browser-issued.ezreport") as package:
    original = package.read("report.pdf")
    archive = package.read("archive").decode("utf-8-sig")
printed = (root / "browser-printed.pdf").read_bytes()
assert original == printed, "打印窗口使用的不是签发时保存的 PDF 原件"
assert hashlib.sha256(original).hexdigest().upper() in archive
reader = PdfReader(root / "browser-printed.pdf")
assert len(reader.pages) == 1
text = "\n".join(page.extract_text() for page in reader.pages)
assert "模拟报告患者" in text and "模拟医师" in text
assert "未经医师审核" not in text
for value in expected:
    assert value in text, f"正式报告缺少预期内容：{value}"
evaluation = PdfReader(root / "browser-evaluation.pdf")
for page in evaluation.pages:
    assert "仅供教学或功能评估使用" in page.extract_text()
    assert "未经医师审核签发" in page.extract_text()
print(f"{scale}：报告内容、水印、签发指纹及打印窗口原件一致性通过。")
