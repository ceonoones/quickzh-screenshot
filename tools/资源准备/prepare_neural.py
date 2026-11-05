"""Build the embedded OCR bundle; only the resource preparation needs Python."""
import hashlib
import json
import pathlib
import urllib.request
import zipfile

ROOT = pathlib.Path(__file__).resolve().parent
SOURCE = ROOT / 'downloads' / 'rapid' / 'RapidOCR-json_v0.2.0'
MODELS = [
    'ch_PP-OCRv4_det_infer.onnx', 'ch_ppocr_mobile_v2.0_cls_infer.onnx',
    'en_v5.onnx', 'en_v5.txt', 'eslav_v5.onnx', 'eslav_v5.txt',
    'latin_v5.onnx', 'latin_v5.txt',
    'rec_ch_PP-OCRv4_infer.onnx', 'dict_chinese.txt',
    'rec_japan_PP-OCRv3_infer.onnx', 'dict_japan.txt',
    'rec_korean_PP-OCRv3_infer.onnx', 'dict_korean.txt',
]
licenses = {
    'RapidOCR-json-LICENSE.txt': 'https://raw.githubusercontent.com/hiroi-sora/RapidOCR-json/main/LICENSE',
    'RapidOcrOnnx-LICENSE.txt': 'https://raw.githubusercontent.com/RapidAI/RapidOcrOnnx/main/LICENSE',
    'PaddleOCR-LICENSE.txt': 'https://raw.githubusercontent.com/PaddlePaddle/PaddleOCR/main/LICENSE',
    'ONNXRuntime-LICENSE.txt': 'https://raw.githubusercontent.com/microsoft/onnxruntime/main/LICENSE',
    'ONNXRuntime-ThirdPartyNotices.txt': 'https://raw.githubusercontent.com/microsoft/onnxruntime/main/ThirdPartyNotices.txt',
    'OpenCV-LICENSE.txt': 'https://raw.githubusercontent.com/opencv/opencv/4.x/LICENSE',
}
manifest = {
    'runtime': 'https://github.com/hiroi-sora/RapidOCR-json/releases/tag/v0.2.0',
    'detector': 'PP-OCRv4 mobile, native ONNX CPU',
    'recognizers': 'PP-OCRv5 English, East Slavic and Latin; PP-OCRv4 Chinese; PP-OCRv3 Japanese/Korean',
    'v5_models': 'https://huggingface.co/monkt/paddleocr-onnx',
    'fallback': 'Tesseract 5.4 / tessdata_fast for Arabic and native-engine failures',
    'sha256': {},
}
with zipfile.ZipFile(ROOT / 'engine-v3.zip', 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as out:
    with zipfile.ZipFile(ROOT / 'engine.zip') as old:
        for item in old.infolist():
            out.writestr(item.filename, old.read(item.filename))
    files = [SOURCE / 'RapidOCR-json.exe'] + [SOURCE / 'models' / name for name in MODELS]
    for file in files:
        data = file.read_bytes()
        name = 'neural/' + file.relative_to(SOURCE).as_posix()
        out.writestr(name, data)
        manifest['sha256'][name] = hashlib.sha256(data).hexdigest()
    for name, url in licenses.items():
        data = urllib.request.urlopen(url, timeout=30).read()
        out.writestr('licenses/' + name, data)
    out.writestr('licenses/本地识别引擎来源.json', json.dumps(manifest, ensure_ascii=False, indent=2))
print('engine-v3.zip', (ROOT / 'engine-v3.zip').stat().st_size)
