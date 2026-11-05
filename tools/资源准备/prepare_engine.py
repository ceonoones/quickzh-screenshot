import concurrent.futures
import pathlib
import shutil
import urllib.request
import zipfile

ROOT = pathlib.Path(__file__).resolve().parent
ENGINE = ROOT / 'engine'
(ENGINE / 'tessdata').mkdir(parents=True, exist_ok=True)
SOURCE = ROOT / 'downloads' / 'tesseract'
for item in list(SOURCE.glob('*.dll')) + [SOURCE / 'tesseract.exe']:
    shutil.copy2(item, ENGINE / item.name)

LANGUAGES = ['eng', 'rus', 'ukr', 'deu', 'fra', 'spa', 'por', 'ita', 'jpn', 'kor', 'chi_sim', 'ara']

def download(lang):
    target = ENGINE / 'tessdata' / (lang + '.traineddata')
    if not target.exists():
        urllib.request.urlretrieve('https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/main/' + target.name, target)
    return lang, target.stat().st_size

with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
    for result in pool.map(download, LANGUAGES):
        print(result, flush=True)

license_dir = ENGINE / 'licenses'
license_dir.mkdir(exist_ok=True)
for name, url in {
    'Tesseract-APACHE-2.0.txt': 'https://raw.githubusercontent.com/tesseract-ocr/tesseract/5.4.0/LICENSE',
    'Tessdata-APACHE-2.0.txt': 'https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/main/LICENSE',
}.items():
    urllib.request.urlretrieve(url, license_dir / name)
if (SOURCE / 'doc').exists():
    shutil.copytree(SOURCE / 'doc', license_dir / 'upstream-doc', dirs_exist_ok=True)

with zipfile.ZipFile(ROOT / 'engine.zip', 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    for item in sorted(ENGINE.rglob('*')):
        if item.is_file():
            archive.write(item, item.relative_to(ENGINE))
print('engine.zip:', (ROOT / 'engine.zip').stat().st_size, flush=True)
