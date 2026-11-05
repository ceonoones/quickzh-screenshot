"""Create allowlisted release artifacts from the exact Git checkout."""
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys
import zipfile

import privacy_check


root = Path(__file__).resolve().parents[1]
version = sys.argv[1]
if not re.fullmatch(r'\d+\.\d+\.\d+', version):
    raise ValueError('Invalid release version')
assembly = (root / 'src/AssemblyInfo.cs').read_text(encoding='utf-8-sig')
if 'AssemblyFileVersion("' + version + '.0")' not in assembly:
    raise ValueError('Assembly version differs from release version')
manifest = json.loads((root / 'resources/ocr-bundle.json').read_text(encoding='utf-8'))
engine = root / 'src/engine.zip'
if hashlib.sha256(engine.read_bytes()).hexdigest() != manifest['sha256']:
    raise ValueError('OCR bundle mismatch')
out = root / 'artifacts'
out.mkdir(exist_ok=True)
prefix = 'QuickZhScreenshot-' + version
exe = out / (prefix + '-win-x64.exe')
shutil.copyfile(root / 'src/快译截图.exe', exe)
portable = out / (prefix + '-win-x64.zip')
with zipfile.ZipFile(portable, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    archive.write(exe, '快译截图.exe')
    for name in ('LICENSE', 'THIRD_PARTY_NOTICES.md', 'docs/使用说明.md'):
        archive.write(root / name, Path(name).name)
    for path in sorted((root / 'third-party').rglob('*')):
        if path.is_file():
            archive.write(path, path.relative_to(root).as_posix())
source = out / (prefix + '-source.zip')
tracked = subprocess.run(['git', '-C', str(root), 'ls-files', '-z'], capture_output=True, check=True).stdout
with zipfile.ZipFile(source, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    for name in tracked.split(b'\0'):
        if name:
            path = name.decode('utf-8')
            archive.write(root / path, 'quickzh-screenshot/' + path)
    archive.write(engine, 'quickzh-screenshot/src/engine.zip', compress_type=zipfile.ZIP_STORED)
files = [exe, portable, source]
for path in files:
    privacy_check.scan(path.name, path.read_bytes())
checksums = ''.join(hashlib.sha256(path.read_bytes()).hexdigest() + '  ' + path.name + '\n' for path in files)
(out / 'SHA256SUMS.txt').write_text(checksums, encoding='ascii')
print('Built, packaged and privacy checked', len(files), 'release artifacts.')
