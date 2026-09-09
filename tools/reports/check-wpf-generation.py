"""核对 WPF 生成探针的七份合成成品；正常退出与视觉验收仍须另外确认。"""
import hashlib
import sys
from pathlib import Path
from urllib.parse import urlsplit

from pypdf import PdfReader

output = Path(sys.argv[1])
expected = {
    "must": ("MUST", "0 分", "营养不良低风险"),
    "nrs-2002": ("NRS 2002", "1 分", "目前没有营养风险"),
    "mna-sf": ("MNA-SF", "13 分", "未提示营养不良风险"),
}
names = {f"{scale}-{kind}.pdf" for scale in expected for kind in ("formal", "evaluation")}
names.add("dris-evaluation.pdf")
hashes = dict(line.split(": ") for line in (output / "generated-sha256.txt").read_text().splitlines())
assert names == set(hashes) == {path.name for path in output.glob("*.pdf")}
requests = (output / "requests.txt").read_text().splitlines()
assert requests and all(urlsplit(url).hostname in ("0.0.0.0", "0.0.0.1") for url in requests)
for asset in ("assessment-report.mjs", "dri-evaluation.mjs", "pdfmake.min.js", "NotoSansCJKsc-Regular.otf"):
    assert any(url.endswith("/" + asset) for url in requests), f"未加载产品资源：{asset}"

for name in sorted(names):
    path = output / name
    assert hashlib.sha256(path.read_bytes()).hexdigest().upper() == hashes[name], name
    pages = PdfReader(path).pages
    assert len(pages) == 1, f"常规合成样本应为一页：{name}"
    text = "\n".join(page.extract_text() for page in pages)
    formal = name.endswith("-formal.pdf")
    if formal:
        for value in ("模拟报告患者", "审核签发：模拟医师", "报告编号", "第 1 版"):
            assert value in text, (name, value)
        assert "未经医师审核" not in text and "仅供教学或功能评估使用" not in text
    else:
        for page in pages:
            for value in ("仅供教学或功能评估使用", "未经医师审核签发", "未关联咨询档案"):
                assert value in page.extract_text(), (name, value)
        assert "报告编号" not in text and "审核签发：模拟医师" not in text
    if name == "dris-evaluation.pdf":
        for value in ("DRIs", "女", "35 岁", "蛋白质", "RNI", "65 g"):
            assert "".join(value.split()) in "".join(text.split()), (name, value)
    else:
        scale = name.rsplit("-", 1)[0]
        for value in (*expected[scale], "70 岁", "165 cm", "60 kg"):
            assert "".join(value.split()) in "".join(text.split()), (name, value)
    for page in pages:
        for reference in page["/Resources"]["/Font"].get_object().values():
            font = reference.get_object()
            for descendant in font.get("/DescendantFonts", [font]):
                descriptor = descendant.get_object()["/FontDescriptor"].get_object()
                embedded = next((descriptor[key] for key in ("/FontFile", "/FontFile2", "/FontFile3") if key in descriptor), None)
                assert embedded is not None and embedded.get_object().get_data(), (name, "缺少嵌入字体")
    print(f"{name}: 内容、水印、字体及 SHA-256 通过")
print("七份成品与本机资源请求记录检查通过；此检查不证明探针正常退出。")
