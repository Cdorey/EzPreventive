"""核对真实浏览器签发包与打印窗口中的原件完全一致。"""
import hashlib
import io
import sys
from pathlib import Path
import zipfile
from pypdf import PdfReader

scale = sys.argv[1] if len(sys.argv) > 1 else "must"
amend = "--revision" in sys.argv[2:]
standalone = "--standalone" in sys.argv[2:]
if scale == "dris":
    reader = PdfReader(Path("tmp/reports/dris/evaluation.pdf"))
    assert len(reader.pages) >= 2
    text = "\n".join(page.extract_text() for page in reader.pages)
    for page in reader.pages:
        assert "仅供教学或功能评估使用" in page.extract_text()
        assert "未经医师审核签发" in page.extract_text()
        assert "未关联咨询档案" in page.extract_text()
    for value in ("80 g", "AMDR 下限", "AMDR 上限", "20 %", "30 %", "PI-NCD", "SPL", "AI", "RNI",
                  "部分参考值需要人工核定", "数据存在冲突，需手工核定", "女", "35 岁", "合成调整记录"):
        assert "".join(value.split()) in "".join(text.split()), f"DRIs 评估稿缺少内容：{value}"
    for index in range(1, 37):
        assert f"合成参考{index:02d}" in text
    assert "报告编号" not in text
    print(f"DRIs：{len(reader.pages)} 页，参考值、上下限、冲突及每页水印通过。")
    sys.exit(0)
expected = {
    "must": ("MUST", "0 分", "营养不良低风险"),
    "nrs-2002": ("NRS 2002", "1 分", "目前没有营养风险"),
    "mna-sf": ("MNA-SF", "13 分", "未提示营养不良风险"),
    "dietary": ("24 小时膳食调查报告", "模拟食物", "247.5 kcal", "150 g", "75%"),
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
    assert scale in ("must", "dietary")
    expected = (("24 小时膳食调查报告", "模拟食物", "495 kcal", "300 g", "第 2 版")
                if scale == "dietary" else ("MUST", "2 分", "营养不良高风险", "第 2 版"))
root = Path("tmp/reports") / (scale + ("-revision" if amend else ""))
with zipfile.ZipFile(root / "browser-issued.ezreport") as package:
    original = package.read("report.pdf")
    archive = package.read("archive").decode("utf-8-sig")
    if amend:
        history = [name for name in package.namelist() if name.startswith("history/")]
        assert len(history) == 1
        assert package.read(history[0]) == (root / "browser-initial.pdf").read_bytes()
        assert "Supersedes" in archive
        if scale == "dietary":
            initial_text = "\n".join(page.extract_text() for page in PdfReader(io.BytesIO(package.read(history[0]))).pages)
            assert "各表列示全部食材" in initial_text and "DRIs 参考资料" in initial_text
printed = (root / "browser-printed.pdf").read_bytes()
assert original == printed, "打印窗口使用的不是签发时保存的 PDF 原件"
assert hashlib.sha256(original).hexdigest().upper() in archive
reader = PdfReader(root / "browser-printed.pdf")
assert len(reader.pages) >= 1 if scale == "dietary" else len(reader.pages) == 1
text = "\n".join(page.extract_text() for page in reader.pages)
assert "模拟报告患者" in text and "模拟医师" in text
assert "未经医师审核" not in text
for value in expected:
    assert value in text, f"正式报告缺少预期内容：{value}"
if scale == "dietary":
    assert ("各表列示前 1 位" if amend else "各表列示全部食材") in text
    assert ("DRIs 参考资料" in text) == (not amend)
    assert "参考范围" in text and "参考类型" in text
evaluation = PdfReader(root / "browser-evaluation.pdf")
if scale == "dietary":
    evaluation_text = "\n".join(page.extract_text() for page in evaluation.pages)
    assert "各表列示前 1 位" in evaluation_text
    assert "DRIs 参考资料" not in evaluation_text
for page in evaluation.pages:
    assert "仅供教学或功能评估使用" in page.extract_text()
    assert "未经医师审核签发" in page.extract_text()
print(f"{scale}：报告内容、水印、签发指纹及打印窗口原件一致性通过。")
