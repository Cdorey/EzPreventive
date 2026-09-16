"""检查合成膳食报告的完整内容、每页标记及嵌入字体；配合 Poppler 逐页视觉检查。"""
import json
import sys
from pathlib import Path
from pypdf import PdfReader

root = Path(sys.argv[1] if len(sys.argv) > 1 else "tmp/reports/dietary")
model = json.loads((root / "dietary-model.json").read_text(encoding="utf-8"))
for name in ("dietary-signed", "dietary-evaluation", "dietary-long"):
    reader = PdfReader(root / f"{name}.pdf")
    text = "\n".join(page.extract_text() for page in reader.pages)
    for value in ("24 小时膳食调查报告", "回顾起止日期未记录", "450 kcal", "150 g", "RNI", "AMDR_L", "蔬菜类", "μg RAE"):
        assert value in text, f"{name} 缺少 {value}"
    for value in ("参考范围", "参考类型", "RNI-UL", "AMDR_L-AMDR_H", "AI-", "-UL", "↑", "↓", "α-生育酚", "水分", "≥", "≤"):
        assert value in text, f"{name} 缺少新版排版内容 {value}"
    assert text.index("宏量营养素与能量") < text.index("矿物质") < text.index("维生素\n") < text.index("水分")
    assert text.index("总维生素E") < text.index("α-生育酚") < text.index("γ-生育酚") < text.index("水分")
    assert "三大宏量营养素的食材贡献排名" in text
    assert "主要营养素的食物来源" not in text
    rankings = text.split("三大宏量营养素的食材贡献排名", 1)[1].split("膳食宝塔比较", 1)[0]
    assert rankings.index("蛋白质") < rankings.index("脂肪") < rankings.index("碳水化合物")
    assert rankings.count("排名") >= 3
    if name == "dietary-long":
        for number in range(1, 61):
            assert f"分页食物 {number} ·" in text
    for page in reader.pages:
        content = page.extract_text()
        assert model["reportNumber"] in content
        if name == "dietary-signed":
            assert "审核签发：模拟医师" in content
            assert "未经医师审核" not in content
        else:
            assert "仅供教学或功能评估使用" in content
            assert "教学 / 功能评估 · 未经医师审核" in content
        for reference in page["/Resources"]["/Font"].get_object().values():
            font = reference.get_object()
            for descendant in font.get("/DescendantFonts", [font]):
                descriptor = descendant.get_object()["/FontDescriptor"].get_object()
                assert any(key in descriptor and descriptor[key].get_object().get_data()
                           for key in ("/FontFile", "/FontFile2", "/FontFile3")), "缺少嵌入字体"
    print(f"{name}: {len(reader.pages)} 页，内容、每页标记及嵌入字体通过")
