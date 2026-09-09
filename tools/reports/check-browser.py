"""核对真实浏览器签发包与打印窗口中的原件完全一致。"""
import hashlib
import sys
from pathlib import Path
import zipfile
from pypdf import PdfReader

scale = sys.argv[1] if len(sys.argv) > 1 else "must"
amend = "--revision" in sys.argv[2:]
standalone = "--standalone" in sys.argv[2:]
expected = {
    "must": ("MUST", "0 分", "营养不良低风险"),
    "nrs-2002": ("NRS 2002", "1 分", "目前没有营养风险"),
    "mna-sf": ("MNA-SF", "13 分", "未提示营养不良风险"),
}[scale]
if standalone:
    root = Path("tmp/reports") / (scale + "-standalone")
    for name in ("incomplete", "complete"):
        reader = PdfReader(root / f"{name}.pdf")
        assert len(reader.pages) == 1
        for page in reader.pages:
            text = page.extract_text()
            assert "仅供教学或功能评估使用" in text
            assert "未经医师审核签发" in text
            assert "独立量表速查" in text and "未关联咨询档案" in text
            assert "报告编号" not in text and "模拟医师" not in text
            for value in expected if name == "complete" else ("尚未完成", "未回答"):
                assert value in text, f"速查评估稿缺少预期内容：{value}"
    print(f"{scale}：独立速查完整/未完成内容和水印通过，无正式报告编号。")
    sys.exit(0)
if amend:
    assert scale == "must"
    expected = ("MUST", "2 分", "营养不良高风险", "第 2 版")
root = Path("tmp/reports") / (scale + ("-revision" if amend else ""))
with zipfile.ZipFile(root / "browser-issued.ezreport") as package:
    original = package.read("report.pdf")
    archive = package.read("archive").decode("utf-8-sig")
    if amend:
        history = [name for name in package.namelist() if name.startswith("history/")]
        assert len(history) == 1
        assert package.read(history[0]) == (root / "browser-initial.pdf").read_bytes()
        assert "Supersedes" in archive
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
