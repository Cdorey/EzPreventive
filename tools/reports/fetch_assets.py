"""固定报告前端依赖；仅开发时联网，应用运行时使用仓库内资源。"""

import base64
import hashlib
import io
from pathlib import Path
import tarfile
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
TARGET = ROOT / "Libraries/EzNutrition.Presentation/wwwroot/reports"
PDFMAKE_VERSION = "0.3.11"
PDFMAKE_INTEGRITY = "Uc49J9hUMyuqJk+U+PxlpBpPr96A4HOOfesGx609EPr2ue82+5/Smq/KTAkEqh0/jUGSi1fumvqZ5yAWijJTJg=="
FONT_REVISION = "f8d157532fbfaeda587e826d4cd5b21a49186f7c"


def download(url: str) -> bytes:
    """下载公开依赖，不发送任何应用或患者信息。"""
    request = urllib.request.Request(url, headers={"User-Agent": "EzNutrition-report-assets"})
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()


def write(relative: str, content: bytes) -> None:
    path = TARGET / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(content)
    print(f"{relative}: {len(content)} bytes; sha256 {hashlib.sha256(content).hexdigest()}", flush=True)


def main() -> None:
    package = download(f"https://registry.npmjs.org/pdfmake/-/pdfmake-{PDFMAKE_VERSION}.tgz")
    if base64.b64encode(hashlib.sha512(package).digest()).decode() != PDFMAKE_INTEGRITY:
        raise ValueError("pdfmake 包与固定的 npm 完整性记录不一致")
    # 只读取明确的文件，不解压任意路径。
    with tarfile.open(fileobj=io.BytesIO(package), mode="r:gz") as archive:
        for source, target in [("package/build/pdfmake.min.js", "vendor/pdfmake.min.js"),
                               ("package/LICENSE", "vendor/pdfmake.LICENSE")]:
            stream = archive.extractfile(source)
            if stream is None:
                raise ValueError(f"依赖包缺少 {source}")
            write(target, stream.read())
    font_root = f"https://raw.githubusercontent.com/notofonts/noto-cjk/{FONT_REVISION}/Sans"
    font = download(font_root + "/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf")
    if hashlib.sha256(font).hexdigest() != "2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b":
        raise ValueError("字体内容与固定的完整性记录不一致")
    write("fonts/NotoSansCJKsc-Regular.otf", font)
    write("fonts/OFL.txt", download(font_root + "/LICENSE"))


if __name__ == "__main__":
    main()
