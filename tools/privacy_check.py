"""Check an explicit source tree or downloaded release, including nested archives.

Python is a development-only dependency. No sensitive matched values are printed.
"""
import io
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import zipfile


def private_values():
    values = {str(Path.cwd()), os.environ.get('USERPROFILE', ''), os.environ.get('COMPUTERNAME', '')}
    local = os.environ.get('LOCALAPPDATA')
    if local:
        config = Path(local) / '快译截图' / '设置.json'
        if config.exists():
            settings = json.loads(config.read_text(encoding='utf-8-sig'))
            values.update(v for k, v in settings.items() if 'key' in k.lower() and isinstance(v, str) and v)
    values = {variant for value in values if len(value) > 5 for variant in (value, value.replace('\\', '/'))}
    return [value.encode(encoding) for value in values for encoding in ('utf-8', 'utf-16le')]


FORBIDDEN = private_values()
PATTERNS = [
    re.compile(rb'sk-[A-Za-z0-9_-]{32,}'),
    re.compile(rb'gh[pousr]_[A-Za-z0-9]{30,}'),
    re.compile(rb'github_pat_[A-Za-z0-9_]{30,}'),
    re.compile(rb'-----BEGIN (?:RSA |OPENSSH |EC )?PRIVATE KEY-----'),
    re.compile(rb'[A-Za-z]:[\\/]Users[\\/][A-Za-z0-9_. -]+[\\/]'),
]
count = 0


def scan(name, data):
    global count
    count += 1
    if any(value in data for value in FORBIDDEN) or any(pattern.search(data) for pattern in PATTERNS):
        raise ValueError('Sensitive identity, private path or credential detected in ' + name)
    if name.lower().endswith('.png'):
        offset = 8
        while offset + 12 <= len(data):
            length = int.from_bytes(data[offset:offset + 4], 'big')
            if data[offset + 4:offset + 8] in (b'tEXt', b'zTXt', b'iTXt', b'eXIf'):
                raise ValueError('Unreviewed image metadata in ' + name)
            offset += length + 12
    if name.lower().endswith('.zip'):
        with zipfile.ZipFile(io.BytesIO(data)) as archive:
            if archive.testzip() is not None:
                raise ValueError('Damaged archive: ' + name)
            for info in archive.infolist():
                if info.is_dir():
                    continue
                lower = info.filename.lower()
                if lower.endswith(('.pdb', '.log')) or any(part in lower for part in ('.git/', '设置.json', 'appdata/')):
                    raise ValueError('Unapproved archive entry in ' + name)
                scan(name + '/' + info.filename, archive.read(info))


def main():
    root = Path(sys.argv[1]).resolve()
    if (root / '.git').exists():
        # Check only the explicit release source list; caches and build outputs are excluded.
        result = subprocess.run(['git', '-C', str(root), 'ls-files', '-z'], check=True, capture_output=True)
        files = [root / name.decode('utf-8') for name in result.stdout.split(b'\0') if name]
        if not files:
            raise ValueError('No tracked source files; stage the explicit release list first.')
    elif root.is_file():
        files = [root]
    else:
        files = [p for p in root.rglob('*') if p.is_file()]
    for path in files:
        scan(path.name, path.read_bytes())
    print('Privacy check passed; inspected entries:', count)


if __name__ == '__main__':
    main()
